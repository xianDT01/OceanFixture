using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class ShipownerBidTests
{
    [Fact]
    public void New_bid_uses_server_identity_and_time_without_changing_cargo_terms()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2943);
        var original = offer.Terms;
        var before = DateTimeOffset.UtcNow;
        var result = new OffersController(store).SubmitBid(offer.Id,
            new(ValidTerms(offer) with { Charterer = "Forged charterer", FreightCurrency = "eur" }, offer.RecapVersion, 119));
        Assert.IsType<OkObjectResult>(result.Result);
        var bid = Assert.Single(offer.Bids);
        Assert.NotEqual(Guid.Empty, bid.Id);
        Assert.Equal(119, bid.ShipownerId);
        Assert.Equal("Northstar Shipping", bid.ShipownerName);
        Assert.Equal(original.Charterer, bid.Terms.Charterer);
        Assert.Equal("EUR", bid.Terms.FreightCurrency);
        Assert.InRange(bid.ReceivedAt, before, DateTimeOffset.UtcNow);
        Assert.Same(original, offer.Terms);
        Assert.Equal(OfferStage.CargoOrder, offer.Stage);
    }

    [Fact]
    public void Updating_own_bid_preserves_id_and_competitors_and_does_not_duplicate_columns()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        var own = offer.Bids.Single(item => item.ShipownerId == 120);
        var competitors = offer.Bids.Where(item => item.ShipownerId != 120).ToArray();
        var columnOrder = offer.Bids.Select(item => item.ShipownerId).ToArray();
        var version = offer.RecapVersion;
        var result = new OffersController(store).SubmitBid(offer.Id, new(ValidTerms(offer) with { Freight = 180000 }, version, 120));
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(3, offer.Bids.Count);
        Assert.Equal(columnOrder, offer.Bids.Select(item => item.ShipownerId).ToArray());
        var updated = Assert.Single(offer.Bids.Where(item => item.ShipownerId == 120));
        Assert.Equal(own.Id, updated.Id);
        Assert.Equal(180000, updated.Terms.Freight);
        Assert.All(competitors, bid => Assert.Contains(bid, offer.Bids));
        Assert.NotEqual(version, offer.RecapVersion);
    }

    [Fact]
    public void Stale_or_duplicate_submission_does_not_overwrite_a_newer_bid()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2943);
        var controller = new OffersController(store);
        var request = new SubmitBidRequest(ValidTerms(offer), offer.RecapVersion, 119);
        Assert.IsType<OkObjectResult>(controller.SubmitBid(offer.Id, request).Result);
        Assert.IsType<ConflictObjectResult>(controller.SubmitBid(offer.Id, request with { Terms = request.Terms with { Freight = 1 } }).Result);
        Assert.Equal(200000, Assert.Single(offer.Bids).Terms.Freight);
    }

    [Theory]
    [InlineData(OfferStage.Negotiation)]
    [InlineData(OfferStage.Operative)]
    [InlineData(OfferStage.Archive)]
    public void Closed_bidding_stage_does_not_accept_edits(OfferStage stage)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        offer.Stage = stage;
        var before = offer.RecapVersion;
        var result = new OffersController(store).SubmitBid(offer.Id, new(ValidTerms(offer), before, 120));
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(before, offer.RecapVersion);
    }

    [Theory]
    [InlineData("empty-freight")]
    [InlineData("zero-freight")]
    [InlineData("negative-freight")]
    [InlineData("quantity")]
    [InlineData("demurrage")]
    [InlineData("currency")]
    [InlineData("unit")]
    [InlineData("cargo")]
    [InlineData("port")]
    [InlineData("laycan")]
    public void Invalid_commercial_terms_do_not_store_a_bid(string invalid)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2943);
        var terms = ValidTerms(offer);
        terms = invalid switch
        {
            "empty-freight" => terms with { Freight = null },
            "zero-freight" => terms with { Freight = 0 },
            "negative-freight" => terms with { Freight = -1 },
            "quantity" => terms with { Quantity = 0 },
            "demurrage" => terms with { Demurrage = -1 },
            "currency" => terms with { FreightCurrency = "12" },
            "unit" => terms with { FreightUnit = " " },
            "cargo" => terms with { Commodity = " " },
            "port" => terms with { PortOfLoading = " " },
            _ => terms with { Laycan = " " }
        };
        Assert.IsType<BadRequestObjectResult>(new OffersController(store).SubmitBid(offer.Id, new(terms, offer.RecapVersion, 119)).Result);
        Assert.Empty(offer.Bids);
    }

    [Theory]
    [InlineData(122)]
    [InlineData(39)]
    [InlineData(999)]
    public void Only_an_invited_approved_shipowner_can_have_a_bid(int owner)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2943);
        store.Companies.Add(new(999, "Unapproved", "Shipowner", "Spain", "", false));
        offer.InvitedShipownerIds.Add(999);
        Assert.IsType<ForbidResult>(new OffersController(store).SubmitBid(offer.Id,
            new(ValidTerms(offer), offer.RecapVersion, owner)).Result);
        Assert.Empty(offer.Bids);
    }

    [Theory]
    [InlineData("Charterer", 39, null, false)]
    [InlineData("Admin", 0, null, false)]
    [InlineData("Shipowner", 119, 120, false)]
    [InlineData("Shipowner", 119, null, true)]
    public async Task Request_filter_assigns_the_session_owner_and_rejects_role_or_identity_spoofing(string role, int company, int? requested, bool allowed)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2943);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim("company_id", company.ToString()) }, "Test"));
        var action = new ActionContext(new DefaultHttpContext { User = principal }, new RouteData(),
            new ControllerActionDescriptor { ActionName = nameof(OffersController.SubmitBid) });
        var context = new ActionExecutingContext(action, new List<IFilterMetadata>(), new Dictionary<string, object?>
        { ["id"] = offer.Id, ["request"] = new SubmitBidRequest(ValidTerms(offer), offer.RecapVersion, requested) }, new object());
        var executed = false;
        await new OfferAccessFilter(store).OnActionExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(action, new List<IFilterMetadata>(), new object()) { Result = new OkResult() });
        });
        Assert.Equal(allowed, executed);
        if (allowed) Assert.Equal(119, Assert.IsType<SubmitBidRequest>(context.ActionArguments["request"]).ShipownerId);
        else Assert.IsType<ForbidResult>(context.Result);
    }

    private static VoyageTerms ValidTerms(CargoOffer offer) => offer.Terms with { Freight = 200000 };
}

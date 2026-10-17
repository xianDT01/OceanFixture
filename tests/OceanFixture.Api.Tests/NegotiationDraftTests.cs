using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class NegotiationDraftTests
{
    public static NegotiationDraftsController Controller(DemoStore store, string user = "alice", string role = "Charterer", int company = 39)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Role, role), new Claim("company_id", company.ToString()) }, "Test"));
        return new(store) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } } };
    }

    private static SaveNegotiationDraftRequest Request(CargoOffer offer) => new(offer.RecapVersion, new() { ["freight"] = "unfinished-" }, ["Private clause"]);
    private static NegotiationDraft Saved(ActionResult<NegotiationDraft> result) => Assert.IsType<NegotiationDraft>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public void Save_keeps_incomplete_fields_private_without_changing_contract_turn_or_confirmation()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        new OffersController(store).ConfirmRecap(offer.Id, new("charterer", "alice", offer.RecapVersion));
        var version = offer.RecapVersion;
        var terms = offer.Terms;
        var saved = Saved(Controller(store).Save(offer.Id, Request(offer)));
        Assert.Equal("unfinished-", saved.Values["freight"]);
        Assert.Equal("alice", saved.UserId);
        Assert.Equal("Charterer", saved.Party);
        Assert.NotEqual(default, saved.SavedAt);
        Assert.Equal(version, offer.RecapVersion);
        Assert.Same(terms, offer.Terms);
        Assert.Single(offer.Confirmations);
        Assert.Equal("Charterer", offer.NegotiationTurn);
        Assert.Empty(offer.NegotiationHistory);
        saved.Values["freight"] = "Response mutation";
        Assert.Equal("unfinished-", offer.NegotiationDrafts[0].Values["freight"]);
    }

    [Fact]
    public void Draft_is_visible_only_to_its_author_and_never_in_general_offer_responses()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        Saved(Controller(store).Save(offer.Id, Request(offer)));
        foreach (var controller in new[] { Controller(store, "bob"), Controller(store, "owner", "Shipowner", 119) })
            Assert.Null(Assert.IsType<OkObjectResult>(controller.Get(offer.Id).Result).Value);
        foreach (var role in new[] { "Charterer", "Shipowner", "Admin" })
        {
            var principal = Controller(store, "alice", role, role == "Shipowner" ? 119 : 39).User;
            var projected = new OfferAccess(principal).Project(store.Snapshot().Offers.Single(item => item.Id == offer.Id));
            Assert.Empty(projected.NegotiationDrafts);
        }
        Assert.Single(offer.NegotiationDrafts);
    }

    [Theory]
    [InlineData("Charterer", 40)]
    [InlineData("Shipowner", 120)]
    [InlineData("Admin", 39)]
    public void Foreign_companies_and_admin_cannot_read_write_or_delete_drafts(string role, int company)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var controller = Controller(store, "other", role, company);
        Assert.IsType<NotFoundResult>(controller.Get(offer.Id).Result);
        Assert.IsType<NotFoundResult>(controller.Save(offer.Id, Request(offer)).Result);
        Assert.IsType<NotFoundResult>(controller.Delete(offer.Id));
    }

    [Fact]
    public void Save_rejects_stale_version_wrong_turn_invalid_fields_and_closed_stage()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var controller = Controller(store);
        Assert.IsType<ConflictObjectResult>(controller.Save(offer.Id, Request(offer) with { BaseVersion = "old" }).Result);
        Assert.IsType<ForbidResult>(Controller(store, "owner", "Shipowner", 119).Save(offer.Id, Request(offer)).Result);
        Assert.IsType<BadRequestObjectResult>(controller.Save(offer.Id, Request(offer) with { Values = new() { ["stage"] = "Operative" } }).Result);
        offer.Stage = OfferStage.Operative;
        Assert.IsType<BadRequestObjectResult>(controller.Save(offer.Id, Request(offer)).Result);
        Assert.Empty(offer.NegotiationDrafts);
    }

    [Fact]
    public void Save_updates_only_own_draft_and_delete_does_not_delete_another_users_draft()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        Saved(Controller(store).Save(offer.Id, Request(offer)));
        Saved(Controller(store, "bob").Save(offer.Id, Request(offer)));
        Saved(Controller(store).Save(offer.Id, Request(offer) with { Values = new() { ["freight"] = "123" } }));
        Assert.Equal(2, offer.NegotiationDrafts.Count);
        Assert.IsType<NoContentResult>(Controller(store).Delete(offer.Id));
        Assert.Equal("bob", Assert.Single(offer.NegotiationDrafts).UserId);
        Assert.IsType<NoContentResult>(Controller(store).Delete(offer.Id));
    }

    [Fact]
    public void Sending_counter_removes_senders_draft_but_keeps_others_as_stale()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        Saved(Controller(store).Save(offer.Id, Request(offer)));
        Saved(Controller(store, "bob").Save(offer.Id, Request(offer)));
        Assert.IsType<OkObjectResult>(new OffersController(store).SendNegotiatedCounter(offer.Id,
            new(offer.Terms, [], [], offer.RecapVersion) { Party = "Charterer", SubmittedBy = "alice" }).Result);
        var remaining = Assert.Single(offer.NegotiationDrafts);
        Assert.Equal("bob", remaining.UserId);
        Assert.NotEqual(offer.RecapVersion, remaining.BaseVersion);
    }
}

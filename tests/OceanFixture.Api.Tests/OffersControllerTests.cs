using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class OffersControllerTests
{
    [Fact]
    public void Dashboard_groups_all_demo_offers_by_stage()
    {
        var controller = CreateController();

        var result = controller.Dashboard();
        var lanes = AssertOk<IReadOnlyCollection<DashboardLane>>(result);

        Assert.Equal(4, lanes.Count);
        Assert.Equal(8, lanes.Sum(lane => lane.Offers.Count));
        Assert.Collection(lanes,
            lane => AssertLane(lane, "CARGO ORDERS", 4),
            lane => AssertLane(lane, "NEGOTIATION", 1),
            lane => AssertLane(lane, "OPERATIVE", 1),
            lane => AssertLane(lane, "ARCHIVE", 2));
    }

    [Fact]
    public void Create_assigns_next_reference_and_persists_offer()
    {
        var store = new DemoStore();
        var controller = new OffersController(store);
        var input = ValidCargoRequest();

        var result = controller.Create(input);
        var response = Assert.IsType<CreatedAtActionResult>(result.Result);
        var created = Assert.IsType<CargoOffer>(response.Value);

        Assert.Equal(2958, created.Reference);
        Assert.Equal("TEST-NEW", created.CustomerReference);
        Assert.Contains(store.Offers, offer => offer.Id == created.Id);
        Assert.Equal(OfferStage.CargoOrder, created.Stage);
        Assert.Equal([119], created.InvitedShipownerIds);
    }

    [Fact]
    public void Firm_offer_selects_bid_and_moves_offer_to_negotiation()
    {
        var store = new DemoStore();
        var offer = Assert.Single(store.Offers.Where(item => item.Reference == 2941));
        var bid = offer.Bids[0];
        var controller = new OffersController(store);

        var result = controller.SelectFirmOffer(offer.Id, bid.ShipownerId, new(offer.RecapVersion));
        var updated = AssertOk<CargoOffer>(result);

        Assert.Equal(OfferStage.Negotiation, updated.Stage);
        Assert.Equal(bid.ShipownerId, updated.SelectedShipownerId);
        Assert.Equal("Shipowner", updated.NegotiationTurn);
        Assert.Equal(bid.Terms, updated.Terms);
    }

    [Fact]
    public void Counter_updates_terms_increments_counter_and_changes_turn()
    {
        var store = new DemoStore();
        var offer = store.Offers[0];
        var controller = new OffersController(store);
        var terms = offer.Terms with { Freight = 123_000 };

        var result = controller.Counter(offer.Id, terms);
        var updated = AssertOk<CargoOffer>(result);

        Assert.Equal(1, updated.Counter);
        Assert.Equal(123_000, updated.Terms.Freight);
        Assert.Equal("Shipowner", updated.NegotiationTurn);
    }

    [Fact]
    public void Recap_requires_two_distinct_parties_before_becoming_operative()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var controller = new OffersController(store);

        var first = controller.ConfirmRecap(offer.Id, new ConfirmRecapRequest("Charterer", "Alice", offer.RecapVersion));
        Assert.Equal(OfferStage.Negotiation, AssertOk<CargoOffer>(first).Stage);

        var second = controller.ConfirmRecap(offer.Id, new ConfirmRecapRequest("Shipowner", "Bob", offer.RecapVersion));
        var updated = AssertOk<CargoOffer>(second);
        Assert.Equal(OfferStage.Operative, updated.Stage);
        Assert.Equal(2, updated.Confirmations.Count);
    }

    [Fact]
    public void Unknown_offer_returns_not_found()
    {
        var result = CreateController().Get(Guid.NewGuid());
        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static OffersController CreateController() => new(new DemoStore());

    [Fact]
    public void Recap_rejects_an_outdated_version_without_recording_confirmation()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var controller = new OffersController(store);
        var version = offer.RecapVersion;
        controller.Counter(offer.Id, offer.Terms with { Freight = 100 });
        Assert.IsType<ConflictObjectResult>(controller.ConfirmRecap(offer.Id, new("charterer", "Alice", version)).Result);
        Assert.Empty(offer.Confirmations);
    }

    [Theory]
    [InlineData("broker", "Alice")]
    [InlineData("charterer", "")]
    public void Recap_rejects_invalid_confirmation_identity(string party, string user)
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        Assert.IsType<BadRequestObjectResult>(new OffersController(store).ConfirmRecap(offer.Id, new(party, user, offer.RecapVersion)).Result);
        Assert.Empty(offer.Confirmations);
    }

    [Fact]
    public void Counter_invalidates_previous_confirmations()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var controller = new OffersController(store);
        controller.ConfirmRecap(offer.Id, new("charterer", "Alice", offer.RecapVersion));
        controller.Counter(offer.Id, offer.Terms with { Freight = 100 });
        Assert.Empty(offer.Confirmations);
        Assert.Equal(OfferStage.Negotiation, offer.Stage);
    }

    [Fact]
    public void Negotiated_counter_saves_clauses_and_terms_and_invalidates_confirmation()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var controller = new OffersController(store);
        var version = offer.RecapVersion;
        controller.ConfirmRecap(offer.Id, new("charterer", "Alice", version));
        var updated = AssertOk(controller.SendNegotiatedCounter(offer.Id,
            new(offer.Terms with { Freight = 120000 }, ["Charterer clause"], ["Owner clause"], version)));
        Assert.Equal(120000, updated.Terms.Freight);
        Assert.Equal("Charterer clause", Assert.Single(updated.ChartererClauses));
        Assert.Equal("Owner clause", Assert.Single(updated.ShipownerClauses));
        Assert.Empty(updated.Confirmations);
        Assert.Equal(1, updated.Counter);
        Assert.NotEqual(version, updated.RecapVersion);
    }

    [Fact]
    public void Negotiated_counter_rejects_repeat_request_even_when_terms_are_unchanged()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var controller = new OffersController(store);
        var request = new NegotiatedCounterRequest(offer.Terms, ["Clause"], [], offer.RecapVersion);
        AssertOk(controller.SendNegotiatedCounter(offer.Id, request));
        Assert.IsType<ConflictObjectResult>(controller.SendNegotiatedCounter(offer.Id, request).Result);
        Assert.Equal(1, offer.Counter);
    }

    [Fact]
    public void Negotiated_counter_rejects_invalid_quantity_without_mutation()
    {
        var store = new DemoStore();
        var offer = store.Offers.First(item => item.Stage == OfferStage.Negotiation);
        var original = offer.Terms;
        var result = new OffersController(store).SendNegotiatedCounter(offer.Id,
            new(original with { Quantity = 0 }, ["Invalid update"], [], offer.RecapVersion));
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Same(original, offer.Terms);
        Assert.Empty(offer.ChartererClauses);
        Assert.Equal(0, offer.Counter);
    }

    [Fact]
    public void Firm_offer_rejects_stale_versions_without_selecting_a_bid()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        var original = offer.Terms;
        var result = new OffersController(store).SelectFirmOffer(offer.Id, offer.Bids[0].ShipownerId, new("stale"));
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Null(offer.SelectedShipownerId);
        Assert.Same(original, offer.Terms);
        Assert.Equal(OfferStage.CargoOrder, offer.Stage);
    }

    [Theory]
    [InlineData(OfferStage.Negotiation)]
    [InlineData(OfferStage.Operative)]
    [InlineData(OfferStage.Archive)]
    public void Firm_offer_does_not_reopen_operations(OfferStage stage)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        offer.Stage = stage;
        var result = new OffersController(store).SelectFirmOffer(offer.Id, offer.Bids[0].ShipownerId, new(offer.RecapVersion));
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(stage, offer.Stage);
        Assert.Null(offer.SelectedShipownerId);
    }

    [Fact]
    public void Firm_offer_rejects_a_shipowner_without_a_bid()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        var result = new OffersController(store).SelectFirmOffer(offer.Id, 999, new(offer.RecapVersion));
        Assert.IsType<NotFoundResult>(result.Result);
        Assert.Null(offer.SelectedShipownerId);
    }

    [Fact]
    public void Updated_bid_invalidates_the_comparison_version()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2941);
        var version = offer.RecapVersion;
        var bid = offer.Bids[0];
        offer.Bids[0] = bid with { Terms = bid.Terms with { Freight = 1 } };
        Assert.NotEqual(version, offer.RecapVersion);
        Assert.IsType<ConflictObjectResult>(new OffersController(store).SelectFirmOffer(offer.Id, bid.ShipownerId, new(version)).Result);
    }

    private static CreateCargoOfferRequest ValidCargoRequest() => new("TEST-NEW", new VoyageTerms
    {
        Charterer = "Customer", Quantity = 8500, Commodity = "Steel", PortOfLoading = "Vigo",
        PortOfDischarge = "Bilbao", Laycan = "20/10/26 - 23/10/26"
    }, [119]);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_rejects_invalid_quantity_without_storing_offer(decimal quantity)
    {
        var store = new DemoStore();
        var request = ValidCargoRequest();
        var result = new OffersController(store).Create(request with { Terms = request.Terms with { Quantity = quantity } });
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(8, store.Offers.Count);
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("charterer")]
    [InlineData("cargo")]
    [InlineData("loading")]
    [InlineData("discharge")]
    [InlineData("laycan")]
    public void Create_rejects_missing_required_text(string field)
    {
        var store = new DemoStore();
        var request = ValidCargoRequest();
        request = field switch
        {
            "reference" => request with { CustomerReference = " " },
            "charterer" => request with { Terms = request.Terms with { Charterer = " " } },
            "cargo" => request with { Terms = request.Terms with { Commodity = " " } },
            "loading" => request with { Terms = request.Terms with { PortOfLoading = " " } },
            "discharge" => request with { Terms = request.Terms with { PortOfDischarge = " " } },
            _ => request with { Terms = request.Terms with { Laycan = " " } }
        };
        Assert.IsType<BadRequestObjectResult>(new OffersController(store).Create(request).Result);
        Assert.Equal(8, store.Offers.Count);
    }

    [Theory]
    [InlineData(999)]
    [InlineData(39)]
    public void Create_rejects_unknown_or_non_shipowner_recipient(int recipient)
    {
        var store = new DemoStore();
        Assert.IsType<BadRequestObjectResult>(new OffersController(store).Create(
            ValidCargoRequest() with { InvitedShipownerIds = [recipient] }).Result);
        Assert.Equal(8, store.Offers.Count);
    }

    [Fact]
    public void Create_requires_at_least_one_approved_shipowner()
    {
        var store = new DemoStore();
        var controller = new OffersController(store);
        Assert.IsType<BadRequestObjectResult>(controller.Create(ValidCargoRequest() with { InvitedShipownerIds = [] }).Result);
        store.Companies.Add(new(999, "Unapproved", "Shipowner", "Spain", "", false));
        Assert.IsType<BadRequestObjectResult>(controller.Create(ValidCargoRequest() with { InvitedShipownerIds = [999] }).Result);
        Assert.Equal(8, store.Offers.Count);
    }

    [Fact]
    public void Create_deduplicates_recipients_and_appears_on_dashboard()
    {
        var store = new DemoStore();
        var controller = new OffersController(store);
        var result = controller.Create(ValidCargoRequest() with { InvitedShipownerIds = [119, 119, 120] });
        var created = Assert.IsType<CargoOffer>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal([119, 120], created.InvitedShipownerIds);
        Assert.Contains(AssertOk(controller.Dashboard()).Single(lane => lane.Key == "CargoOrder").Offers, offer => offer.Id == created.Id);
    }

    [Fact]
    public void Concurrent_creates_assign_unique_references()
    {
        var store = new DemoStore();
        Parallel.For(0, 10, _ => new OffersController(store).Create(ValidCargoRequest()));
        Assert.Equal(18, store.Offers.Count);
        Assert.Equal(18, store.Offers.Select(offer => offer.Reference).Distinct().Count());
    }

    private static T AssertOk<T>(ActionResult<T> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }

    private static void AssertLane(DashboardLane lane, string label, int count)
    {
        Assert.Equal(label, lane.Label);
        Assert.Equal(count, lane.Offers.Count);
    }
}

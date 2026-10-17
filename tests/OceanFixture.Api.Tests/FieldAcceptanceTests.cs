using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class FieldAcceptanceTests
{
    public static FieldAcceptancesController Controller(DemoStore store, string role = "Charterer", int company = 39, string user = "alice") =>
        new(store) { ControllerContext = NegotiationDraftTests.Controller(store, user, role, company).ControllerContext };
    private static CargoOffer Ok(ActionResult<CargoOffer> result) => Assert.IsType<CargoOffer>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public void Both_parties_accept_independently_without_turn_or_contract_changes()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var version = offer.RecapVersion;
        new OffersController(store).ConfirmRecap(offer.Id, new("charterer", "alice", version));
        Ok(Controller(store).Set(offer.Id, "laycan", new(version, true)));
        var response = Ok(Controller(store, "Shipowner", 119, "bob").Set(offer.Id, "laycan", new(version, true)));
        Assert.Equal(2, response.FieldAcceptances.Count);
        Assert.Equal("alice", response.FieldAcceptances.Single(item => item.Party == "Charterer").AcceptedBy);
        Assert.Equal("bob", response.FieldAcceptances.Single(item => item.Party == "Shipowner").AcceptedBy);
        Assert.All(response.FieldAcceptances, item => Assert.NotEqual(default, item.AcceptedAt));
        Assert.Equal(version, offer.RecapVersion);
        Assert.Equal("Charterer", offer.NegotiationTurn);
        Assert.Equal(0, offer.Counter);
        Assert.Single(offer.Confirmations);
        Assert.Empty(offer.NegotiationHistory);
    }

    [Fact]
    public void Repeated_acceptance_is_idempotent_and_withdrawal_affects_only_own_party()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var request = new SetFieldAcceptanceRequest(offer.RecapVersion, true);
        Ok(Controller(store).Set(offer.Id, "freight", request));
        var first = offer.FieldAcceptances.Single();
        Ok(Controller(store, user: "second-user").Set(offer.Id, "freight", request));
        Assert.Equal(first, Assert.Single(offer.FieldAcceptances));
        Ok(Controller(store, "Shipowner", 119, "bob").Set(offer.Id, "freight", request));
        Ok(Controller(store).Set(offer.Id, "freight", request with { Accepted = false }));
        Assert.Equal("Shipowner", Assert.Single(offer.FieldAcceptances).Party);
        Ok(Controller(store).Set(offer.Id, "freight", request with { Accepted = false }));
        Assert.Single(offer.FieldAcceptances);
    }

    [Theory]
    [InlineData("Charterer", 40)]
    [InlineData("Shipowner", 120)]
    [InlineData("Admin", 39)]
    public void Other_companies_and_admin_cannot_accept(string role, int company)
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        Assert.IsType<NotFoundResult>(Controller(store, role, company).Set(offer.Id, "laycan", new(offer.RecapVersion, true)).Result);
        Assert.Empty(offer.FieldAcceptances);
    }

    [Fact]
    public void Stale_unknown_and_closed_requests_cannot_write()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var controller = Controller(store);
        Assert.IsType<ConflictObjectResult>(controller.Set(offer.Id, "laycan", new("stale", true)).Result);
        Assert.IsType<BadRequestObjectResult>(controller.Set(offer.Id, "stage", new(offer.RecapVersion, true)).Result);
        offer.Stage = OfferStage.Operative;
        Assert.IsType<BadRequestObjectResult>(controller.Set(offer.Id, "laycan", new(offer.RecapVersion, true)).Result);
        Assert.Empty(offer.FieldAcceptances);
    }

    [Fact]
    public void Counter_invalidates_changed_fields_for_both_parties_but_keeps_unchanged_acceptance()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var version = offer.RecapVersion;
        Ok(Controller(store).Set(offer.Id, "freight", new(version, true)));
        Ok(Controller(store, "Shipowner", 119, "bob").Set(offer.Id, "freight", new(version, true)));
        Ok(Controller(store).Set(offer.Id, "laycan", new(version, true)));
        Ok(new OffersController(store).SendNegotiatedCounter(offer.Id,
            new(offer.Terms with { Freight = 123456 }, [], [], version) { Party = "Charterer", SubmittedBy = "alice" }));
        Assert.Equal("laycan", Assert.Single(offer.FieldAcceptances).Field);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("freight")]
    [InlineData("demurrage")]
    public void Hash_includes_units_and_currency_but_ignores_decimal_scale(string field)
    {
        var terms = new VoyageTerms { Quantity = 100, Freight = 100, Demurrage = 100 };
        var hash = NegotiationFields.ValueHash(terms, field);
        Assert.Equal(hash, NegotiationFields.ValueHash(terms with { Quantity = 100.00m, Freight = 100.00m, Demurrage = 100.00m }, field));
        var changed = field switch {
            "quantity" => terms with { QuantityUnit = "CBM" },
            "freight" => terms with { FreightCurrency = "EUR" },
            _ => terms with { DemurrageUnit = "DAY" }
        };
        Assert.NotEqual(hash, NegotiationFields.ValueHash(changed, field));
        if (field == "freight") Assert.NotEqual(hash, NegotiationFields.ValueHash(terms with { FreightUnit = "MT" }, field));
    }

    [Fact]
    public void Acceptance_response_never_exposes_private_drafts()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        NegotiationDraftTests.Controller(store).Save(offer.Id, new(offer.RecapVersion, new() { ["freight"] = "private" }, []));
        var response = Ok(Controller(store, "Shipowner", 119, "bob").Set(offer.Id, "laycan", new(offer.RecapVersion, true)));
        Assert.Empty(response.NegotiationDrafts);
        Assert.Single(offer.NegotiationDrafts);
    }
}

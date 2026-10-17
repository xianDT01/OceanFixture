using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class NegotiationHistoryTests
{
    private static OfferAccess Access(string role, int company) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    { new Claim(ClaimTypes.Role, role), new Claim("company_id", company.ToString()) }, "Test")));

    [Fact]
    public void Only_current_party_can_send_but_either_can_confirm()
    {
        var offer = new DemoStore().Offers.Single(item => item.Reference == 2942);
        foreach (var turn in new[] { "Charterer", "Shipowner" })
        {
            offer.NegotiationTurn = turn;
            Assert.Equal(turn == "Charterer", Access("Charterer", 39).Permissions(offer).CanSendCounter);
            Assert.Equal(turn == "Shipowner", Access("Shipowner", 119).Permissions(offer).CanSendCounter);
            Assert.True(Access("Charterer", 39).Permissions(offer).CanConfirmAsCharterer);
            Assert.True(Access("Shipowner", 119).Permissions(offer).CanConfirmAsShipowner);
            Assert.False(Access("Admin", 0).Permissions(offer).CanSendCounter);
            Assert.False(Access("Shipowner", 120).Permissions(offer).CanSendCounter);
        }
    }

    [Fact]
    public void Counter_records_immutable_before_and_after_snapshots_and_alternates_turns()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var controller = new OffersController(store);
        var before = offer.Terms;
        var first = new NegotiatedCounterRequest(before with { Freight = 123 }, ["A"], [], offer.RecapVersion)
        { Party = "Charterer", SubmittedBy = "server-charterer" };
        Assert.IsType<OkObjectResult>(controller.SendNegotiatedCounter(offer.Id, first).Result);
        Assert.Equal("Shipowner", offer.NegotiationTurn);
        var second = new NegotiatedCounterRequest(offer.Terms with { Freight = 456 }, ["A"], ["B"], offer.RecapVersion)
        { Party = "Shipowner", SubmittedBy = "server-owner" };
        Assert.IsType<OkObjectResult>(controller.SendNegotiatedCounter(offer.Id, second).Result);
        Assert.Equal("Charterer", offer.NegotiationTurn);
        Assert.Equal(2, offer.NegotiationHistory.Count);
        var entry = offer.NegotiationHistory[0];
        Assert.Equal(before, entry.Before.Terms);
        Assert.Equal(123, entry.After.Terms.Freight);
        Assert.Empty(entry.Before.ChartererClauses);
        Assert.Equal("A", Assert.Single(entry.After.ChartererClauses));
        Assert.Empty(entry.After.ShipownerClauses);
        Assert.Equal("server-charterer", entry.SubmittedBy);
        Assert.NotEqual(default, entry.SubmittedAt);
        Assert.Equal(1, entry.Counter);
        Assert.Equal(2, offer.NegotiationHistory[1].Counter);
        Assert.NotEqual(entry.Id, offer.NegotiationHistory[1].Id);
        // Mutating a detached response must not mutate the live audit data.
        var detached = store.Snapshot().Offers.Single(item => item.Id == offer.Id);
        detached.NegotiationHistory[0].After.ChartererClauses[0] = "Forged";
        Assert.Equal("A", entry.After.ChartererClauses[0]);
    }

    [Fact]
    public void Wrong_turn_with_current_version_cannot_write_history_or_conditions()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var version = offer.RecapVersion;
        var result = new OffersController(store).SendNegotiatedCounter(offer.Id,
            new(offer.Terms with { Freight = 1 }, [], [], version) { Party = "Shipowner", SubmittedBy = "owner" });
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(version, offer.RecapVersion);
        Assert.Empty(offer.NegotiationHistory);
    }

    [Fact]
    public void Client_json_cannot_supply_audit_identity()
    {
        var request = JsonSerializer.Deserialize<NegotiatedCounterRequest>(
            """{"Terms":{},"ChartererClauses":[],"ShipownerClauses":[],"VersionHash":"v1","Party":"Charterer","SubmittedBy":"forged"}""")!;
        Assert.Null(request.Party);
        Assert.Null(request.SubmittedBy);
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        Assert.IsType<ForbidResult>(new OffersController(store).SendNegotiatedCounter(offer.Id,
            request with { Terms = offer.Terms, VersionHash = offer.RecapVersion }).Result);
        Assert.Empty(offer.NegotiationHistory);
    }

    [Fact]
    public void Old_data_defaults_to_empty_history_without_changing_recap_version()
    {
        var original = new DemoStore().Offers.Single(item => item.Reference == 2942);
        var oldJson = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(original))!.AsObject();
        oldJson.Remove("NegotiationHistory");
        var restored = JsonSerializer.Deserialize<CargoOffer>(oldJson.ToJsonString())!;
        Assert.Empty(restored.NegotiationHistory);
        var version = restored.RecapVersion;
        restored.NegotiationHistory.Add(new(Guid.NewGuid(), 1, "Charterer", "user", DateTimeOffset.UtcNow,
            new(restored.Terms, [], []), new(restored.Terms, [], [])));
        Assert.Equal(version, restored.RecapVersion);
    }
}

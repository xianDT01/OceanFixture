using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "oceanfixture-tests-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "state.db");

    private T WithStore<T>(Func<DemoStore, T> action)
    {
        using var persistence = new SqliteStatePersistence(DatabasePath);
        return action(new DemoStore(persistence));
    }

    [Fact]
    public void Field_acceptance_survives_restart_and_failed_accept_or_withdraw_rolls_back()
    {
        var id = WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2942);
            Assert.IsType<OkObjectResult>(FieldAcceptanceTests.Controller(store).Set(offer.Id, "laycan", new(offer.RecapVersion, true)).Result);
            return offer.Id;
        });
        using var persistence = new SqliteStatePersistence(DatabasePath);
        var store = new DemoStore(persistence);
        var offer = store.Offers.Single(item => item.Id == id);
        Assert.Equal("laycan", Assert.Single(offer.FieldAcceptances).Field);
        var before = Snapshot(store);
        ExecuteSql("CREATE TRIGGER RejectAcceptanceWrite BEFORE UPDATE ON ApplicationState BEGIN SELECT RAISE(ABORT, 'test write failure'); END;");
        var controller = FieldAcceptanceTests.Controller(store);
        Assert.Throws<PersistenceWriteException>(() => controller.Set(id, "laycan", new(offer.RecapVersion, false)));
        Assert.Equal(before, Snapshot(store));
        Assert.Throws<PersistenceWriteException>(() => controller.Set(id, "freight", new(offer.RecapVersion, true)));
        Assert.Equal(before, Snapshot(store));
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Load()));
    }

    [Fact]
    public void Private_draft_survives_restart_and_save_delete_failures_roll_back()
    {
        var id = WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2942);
            var result = NegotiationDraftTests.Controller(store).Save(offer.Id, new(offer.RecapVersion,
                new() { ["freight"] = "unfinished-" }, ["Private"]));
            Assert.IsType<OkObjectResult>(result.Result);
            return offer.Id;
        });
        using var persistence = new SqliteStatePersistence(DatabasePath);
        var store = new DemoStore(persistence);
        Assert.Equal("unfinished-", store.Offers.Single(item => item.Id == id).NegotiationDrafts.Single().Values["freight"]);
        ExecuteSql("CREATE TRIGGER RejectDraftWrite BEFORE UPDATE ON ApplicationState BEGIN SELECT RAISE(ABORT, 'test write failure'); END;");
        var before = Snapshot(store);
        var controller = NegotiationDraftTests.Controller(store);
        Assert.Throws<PersistenceWriteException>(() => controller.Delete(id));
        Assert.Equal(before, Snapshot(store));
        var version = store.Offers.Single(item => item.Id == id).RecapVersion;
        Assert.Throws<PersistenceWriteException>(() => controller.Save(id, new(version, new() { ["freight"] = "123" }, [])));
        Assert.Equal(before, Snapshot(store));
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Load()));
    }

    [Fact]
    public void Restart_preserves_seed_ids_bids_versions_and_companies_without_duplicates()
    {
        var before = WithStore(Snapshot);
        var after = WithStore(Snapshot);
        Assert.Equal(before, after);
        Assert.Equal(8, WithStore(store => store.Offers.Count));
    }

    [Fact]
    public void Created_cargo_and_next_reference_survive_restart()
    {
        var id = WithStore(store =>
        {
            var result = new OffersController(store).Create(CargoRequest());
            return Assert.IsType<CargoOffer>(Assert.IsType<CreatedAtActionResult>(result.Result).Value).Id;
        });
        WithStore(store =>
        {
            var saved = Assert.Single(store.Offers.Where(offer => offer.Id == id));
            Assert.Equal("PERSISTED CARGO", saved.CustomerReference);
            Assert.Equal(8500, saved.Terms.Quantity);
            Assert.Equal([119, 120], saved.InvitedShipownerIds);
            var next = new OffersController(store).Create(CargoRequest());
            Assert.Equal(2959, Assert.IsType<CargoOffer>(Assert.IsType<CreatedAtActionResult>(next.Result).Value).Reference);
            return true;
        });
    }

    [Fact]
    public void Full_workflow_survives_a_restart_between_each_step()
    {
        var id = WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2941);
            AssertOk(new OffersController(store).SelectFirmOffer(offer.Id, 120, new(offer.RecapVersion)));
            return offer.Id;
        });
        var version = WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Id == id);
            Assert.Equal(120, offer.SelectedShipownerId);
            Assert.Equal(195000, offer.Terms.Freight);
            var updated = AssertOk(new OffersController(store).SendNegotiatedCounter(id,
                new(offer.Terms with { Freight = 190000 }, ["Charterer clause"], ["Owner clause"], offer.RecapVersion) { Party = offer.NegotiationTurn, SubmittedBy = "Bob" }));
            return updated.RecapVersion;
        });
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Id == id);
            Assert.Equal(version, offer.RecapVersion);
            Assert.Equal(1, offer.Counter);
            var history = Assert.Single(offer.NegotiationHistory);
            Assert.Equal("Shipowner", history.Party);
            Assert.Equal("Bob", history.SubmittedBy);
            Assert.Equal(195000, history.Before.Terms.Freight);
            Assert.Equal(190000, history.After.Terms.Freight);
            Assert.Equal("Charterer clause", Assert.Single(offer.ChartererClauses));
            Assert.Equal("Owner clause", Assert.Single(offer.ShipownerClauses));
            AssertOk(new OffersController(store).ConfirmRecap(id, new("charterer", "Alice", version)));
            return true;
        });
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Id == id);
            Assert.Equal(OfferStage.Negotiation, offer.Stage);
            Assert.Equal("Alice", Assert.Single(offer.Confirmations).ConfirmedBy);
            AssertOk(new OffersController(store).ConfirmRecap(id, new("shipowner", "Bob", version)));
            return true;
        });
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Id == id);
            Assert.Equal(OfferStage.Operative, offer.Stage);
            Assert.Equal(190000, offer.Terms.Freight);
            Assert.Equal(2, offer.Confirmations.Count);
            Assert.All(offer.Confirmations, confirmation =>
            {
                Assert.Equal(version, confirmation.VersionHash);
                Assert.NotEqual(default, confirmation.ConfirmedAt);
            });
            return true;
        });
    }

    [Fact]
    public void New_counter_persistently_invalidates_old_confirmations()
    {
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2942);
            var controller = new OffersController(store);
            AssertOk(controller.ConfirmRecap(offer.Id, new("charterer", "Alice", offer.RecapVersion)));
            AssertOk(controller.SendNegotiatedCounter(offer.Id,
                new(offer.Terms with { Freight = 100 }, [], [], offer.RecapVersion) { Party = offer.NegotiationTurn, SubmittedBy = "Alice" }));
            return true;
        });
        Assert.Empty(WithStore(store => store.Offers.Single(item => item.Reference == 2942).Confirmations));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("select")]
    [InlineData("bid")]
    [InlineData("counter")]
    [InlineData("negotiated-counter")]
    [InlineData("confirm")]
    [InlineData("second-confirmation")]
    public void Database_failure_rolls_back_memory_and_disk_for_every_mutation(string operation)
    {
        using var persistence = new SqliteStatePersistence(DatabasePath);
        var store = new DemoStore(persistence);
        var controller = new OffersController(store);
        var offer = store.Offers.Single(item => item.Reference == (operation is "select" or "bid" ? 2941 : 2942));
        if (operation == "second-confirmation")
            AssertOk(controller.ConfirmRecap(offer.Id, new("charterer", "Alice", offer.RecapVersion)));
        if (operation == "negotiated-counter")
        {
            Assert.IsType<OkObjectResult>(NegotiationDraftTests.Controller(store, "Alice").Save(offer.Id,
                new(offer.RecapVersion, new() { ["freight"] = "unfinished-" }, ["Private"])).Result);
            Assert.IsType<OkObjectResult>(FieldAcceptanceTests.Controller(store).Set(offer.Id, "freight", new(offer.RecapVersion, true)).Result);
        }
        var before = Snapshot(store);
        // A real SQLite write failure exercises transaction rollback, not only a mock.
        ExecuteSql("CREATE TRIGGER RejectWrite BEFORE UPDATE ON ApplicationState BEGIN SELECT RAISE(ABORT, 'test write failure'); END;");
        Assert.Throws<PersistenceWriteException>(() =>
        {
            switch (operation)
            {
                case "create": controller.Create(CargoRequest()); break;
                case "select": controller.SelectFirmOffer(offer.Id, 120, new(offer.RecapVersion)); break;
                case "bid": controller.SubmitBid(offer.Id, new(offer.Terms with { Freight = 100 }, offer.RecapVersion, 120)); break;
                case "counter": controller.Counter(offer.Id, offer.Terms with { Freight = 100 }); break;
                case "negotiated-counter":
                    controller.SendNegotiatedCounter(offer.Id,
                    new(offer.Terms with { Freight = 100 }, ["Changed clause"], [], offer.RecapVersion) { Party = offer.NegotiationTurn, SubmittedBy = "Alice" }); break;
                default: controller.ConfirmRecap(offer.Id, new("shipowner", "Bob", offer.RecapVersion)); break;
            }
        });
        Assert.Equal(before, Snapshot(store));
        Assert.Equal(before, JsonSerializer.Serialize(persistence.Load()));
    }

    [Fact]
    public void Submitted_and_updated_bid_survives_restart_without_changing_original_cargo()
    {
        var id = WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2943);
            AssertOk(new OffersController(store).SubmitBid(offer.Id, new(offer.Terms with { Freight = 1000 }, offer.RecapVersion, 119)));
            return Assert.Single(offer.Bids).Id;
        });
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2943);
            Assert.Equal(id, Assert.Single(offer.Bids).Id);
            AssertOk(new OffersController(store).SubmitBid(offer.Id, new(offer.Terms with { Freight = 2000 }, offer.RecapVersion, 119)));
            return true;
        });
        WithStore(store =>
        {
            var offer = store.Offers.Single(item => item.Reference == 2943);
            Assert.Equal(OfferStage.CargoOrder, offer.Stage);
            Assert.Null(offer.Terms.Freight);
            var bid = Assert.Single(offer.Bids);
            Assert.Equal(id, bid.Id);
            Assert.Equal(2000, bid.Terms.Freight);
            return true;
        });
    }

    [Fact]
    public void Corrupt_payload_is_not_silently_replaced_with_demo_data()
    {
        WithStore(store => store.Offers.Count);
        ExecuteSql("UPDATE ApplicationState SET Payload = 'invalid-json' WHERE Id = 1");
        Assert.Throws<JsonException>(() => WithStore(store => store.Offers.Count));
        Assert.Equal("invalid-json", Scalar("SELECT Payload FROM ApplicationState WHERE Id = 1"));
    }

    [Fact]
    public void Incomplete_saved_state_is_not_reseeded()
    {
        WithStore(store => store.Offers.Count);
        ExecuteSql("UPDATE ApplicationState SET Payload = '{}' WHERE Id = 1");
        Assert.Throws<InvalidDataException>(() => WithStore(store => store.Offers.Count));
        Assert.Equal("{}", Scalar("SELECT Payload FROM ApplicationState WHERE Id = 1"));
    }

    [Fact]
    public void Read_snapshots_cannot_mutate_the_live_or_saved_operation()
    {
        WithStore(store =>
        {
            var snapshot = store.Snapshot();
            snapshot.Offers[0].CustomerReference = "Uncommitted edit";
            snapshot.Offers.Clear();
            Assert.Equal(8, store.Offers.Count);
            Assert.NotEqual("Uncommitted edit", store.Offers[0].CustomerReference);
            return true;
        });
        Assert.Equal(8, WithStore(store => store.Offers.Count));
    }

    [Fact]
    public void Unsupported_schema_fails_without_overwriting_data()
    {
        WithStore(store => store.Offers.Count);
        ExecuteSql("UPDATE ApplicationState SET SchemaVersion = 99 WHERE Id = 1");
        Assert.Throws<InvalidDataException>(() => WithStore(store => store.Offers.Count));
        Assert.Equal(99L, Scalar("SELECT SchemaVersion FROM ApplicationState WHERE Id = 1"));
    }

    [Fact]
    public void Empty_saved_offers_are_not_reseeded()
    {
        using (var persistence = new SqliteStatePersistence(DatabasePath))
        {
            var store = new DemoStore(persistence);
            store.Commit(() => store.Offers.Clear());
        }
        Assert.Equal(0, WithStore(store => store.Offers.Count));
    }

    [Fact]
    public void Second_api_cannot_overwrite_the_same_database_and_lease_is_released_on_disposal()
    {
        using (var first = new SqliteStatePersistence(DatabasePath))
            Assert.Throws<IOException>(() => new SqliteStatePersistence(DatabasePath));
        using var reopened = new SqliteStatePersistence(DatabasePath);
        Assert.Null(reopened.Load());
    }

    private static CreateCargoOfferRequest CargoRequest() => new("PERSISTED CARGO", new()
    {
        Charterer = "Customer",
        Quantity = 8500,
        Commodity = "Steel",
        PortOfLoading = "Vigo",
        PortOfDischarge = "Bilbao",
        Laycan = "20/10/26 - 23/10/26"
    }, [119, 120]);

    private static string Snapshot(DemoStore store) => JsonSerializer.Serialize(new StoredState(store.Companies, store.Offers));
    private static CargoOffer AssertOk(ActionResult<CargoOffer> result) =>
        Assert.IsType<CargoOffer>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private void ExecuteSql(string sql) => Scalar(sql);
    private object? Scalar(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        // Only exact files owned by this test; no recursive deletion.
        foreach (var suffix in new[] { "", ".lock", "-journal", "-wal", "-shm" })
            File.Delete(DatabasePath + suffix);
        if (Directory.Exists(directory)) Directory.Delete(directory);
    }
}

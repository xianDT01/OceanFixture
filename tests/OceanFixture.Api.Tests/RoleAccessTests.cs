using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;
using Xunit;

namespace OceanFixture.Api.Tests;

public sealed class RoleAccessTests
{
    private static ClaimsPrincipal User(string role, int? company = null) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "server-user"), new Claim(ClaimTypes.Name, "Server name"),
        new Claim(ClaimTypes.Role, role), new Claim("company_id", company?.ToString() ?? "")
    }, "Test"));

    [Theory]
    [InlineData("Charterer", 39, true)]
    [InlineData("Charterer", 40, false)]
    [InlineData("Shipowner", 119, true)]
    [InlineData("Shipowner", 999, false)]
    [InlineData("Admin", 0, true)]
    [InlineData("Unknown", 39, false)]
    public void Cargo_visibility_depends_on_role_and_company(string role, int company, bool expected)
    {
        var offer = new DemoStore().Offers.First(item => item.Stage == OfferStage.CargoOrder);
        Assert.Equal(expected, new OfferAccess(User(role, company)).CanRead(offer));
    }

    [Fact]
    public void Non_selected_shipowner_loses_access_to_negotiation()
    {
        var offer = new DemoStore().Offers.Single(item => item.Reference == 2942);
        Assert.True(new OfferAccess(User("Shipowner", 119)).CanRead(offer));
        Assert.False(new OfferAccess(User("Shipowner", 120)).CanRead(offer));
        Assert.False(new OfferAccess(new ClaimsPrincipal()).CanRead(offer));
    }

    [Fact]
    public void Shipowner_projection_does_not_expose_competitors_bids()
    {
        var store = new DemoStore();
        var offer = store.Snapshot().Offers.Single(item => item.Reference == 2941);
        var serverVersion = offer.RecapVersion;
        var projected = new OfferAccess(User("Shipowner", 120)).Project(offer);
        Assert.Equal(serverVersion, projected.RecapVersion);
        Assert.Equal(120, Assert.Single(projected.Bids).ShipownerId);
        Assert.False(projected.Permissions!.CanSelectFirmOffer);
        Assert.Equal(3, store.Offers.Single(item => item.Reference == 2941).Bids.Count);
    }

    [Fact]
    public void Only_its_own_party_can_confirm_and_admin_cannot_sign_for_either_party()
    {
        var offer = new DemoStore().Offers.Single(item => item.Reference == 2942);
        var charterer = new OfferAccess(User("Charterer", 39)).Permissions(offer);
        var owner = new OfferAccess(User("Shipowner", 119)).Permissions(offer);
        var admin = new OfferAccess(User("Admin")).Permissions(offer);
        Assert.True(charterer.CanConfirmAsCharterer); Assert.False(charterer.CanConfirmAsShipowner);
        Assert.True(owner.CanConfirmAsShipowner); Assert.False(owner.CanConfirmAsCharterer);
        Assert.False(admin.CanConfirmAsCharterer); Assert.False(admin.CanConfirmAsShipowner);
    }

    [Fact]
    public async Task Filter_assigns_company_and_charterer_from_the_session_not_from_form_text()
    {
        var store = new DemoStore();
        var request = new CreateCargoOfferRequest("Test", new() { Charterer = "Forged company" }, [119]);
        var result = await RunFilter(store, User("Charterer", 40), nameof(OffersController.Create),
            new() { ["request"] = request });
        Assert.True(result.Executed);
        var bound = Assert.IsType<CreateCargoOfferRequest>(result.Context.ActionArguments["request"]);
        Assert.Equal(40, bound.ChartererCompanyId);
        Assert.Equal("Charterer Two", bound.Terms.Charterer);
    }

    [Fact]
    public async Task Filter_rejects_forged_company_and_shipowner_cargo_creation()
    {
        var store = new DemoStore();
        var request = new CreateCargoOfferRequest("Test", new(), [119], 39);
        foreach (var actor in new[] { User("Charterer", 40), User("Shipowner", 119) })
        {
            var result = await RunFilter(store, actor, nameof(OffersController.Create), new() { ["request"] = request });
            Assert.False(result.Executed);
            Assert.IsType<ForbidResult>(result.Context.Result);
        }
    }

    [Fact]
    public async Task Filter_returns_not_found_for_foreign_operations_by_id_or_reference()
    {
        var store = new DemoStore();
        var offer = store.Offers[0];
        foreach (var args in new[] { new Dictionary<string, object?> { ["id"] = offer.Id }, new() { ["reference"] = offer.Reference } })
        {
            var result = await RunFilter(store, User("Charterer", 40), nameof(OffersController.Get), args);
            Assert.False(result.Executed);
            Assert.IsType<NotFoundResult>(result.Context.Result);
        }
    }

    [Fact]
    public async Task Filter_ignores_client_supplied_signer_identity()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var result = await RunFilter(store, User("Charterer", 39), nameof(OffersController.ConfirmRecap),
            new() { ["id"] = offer.Id, ["request"] = new ConfirmRecapRequest("charterer", "Forged signer", offer.RecapVersion) });
        Assert.True(result.Executed);
        Assert.Equal("server-user", Assert.IsType<ConfirmRecapRequest>(result.Context.ActionArguments["request"]).ConfirmedBy);
    }

    [Fact]
    public async Task Filter_blocks_confirming_as_the_other_party()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var result = await RunFilter(store, User("Charterer", 39), nameof(OffersController.ConfirmRecap),
            new() { ["id"] = offer.Id, ["request"] = new ConfirmRecapRequest("shipowner", "Forged signer", offer.RecapVersion) });
        Assert.False(result.Executed);
        Assert.IsType<ForbidResult>(result.Context.Result);
    }

    [Fact]
    public async Task Filter_assigns_counter_actor_and_blocks_wrong_turn()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var request = new NegotiatedCounterRequest(offer.Terms, [], [], offer.RecapVersion)
        { Party = "Shipowner", SubmittedBy = "forged" };
        var allowed = await RunFilter(store, User("Charterer", 39), nameof(OffersController.SendNegotiatedCounter),
            new() { ["id"] = offer.Id, ["request"] = request });
        Assert.True(allowed.Executed);
        var bound = Assert.IsType<NegotiatedCounterRequest>(allowed.Context.ActionArguments["request"]);
        Assert.Equal("Charterer", bound.Party);
        Assert.Equal("server-user", bound.SubmittedBy);
        var denied = await RunFilter(store, User("Shipowner", 119), nameof(OffersController.SendNegotiatedCounter),
            new() { ["id"] = offer.Id, ["request"] = request });
        Assert.False(denied.Executed);
        Assert.IsType<ForbidResult>(denied.Context.Result);
    }

    [Fact]
    public async Task Filter_blocks_modifying_counterparty_clauses()
    {
        var store = new DemoStore();
        var offer = store.Offers.Single(item => item.Reference == 2942);
        var request = new NegotiatedCounterRequest(offer.Terms, [], ["Forged owner clause"], offer.RecapVersion);
        var result = await RunFilter(store, User("Charterer", 39), nameof(OffersController.SendNegotiatedCounter),
            new() { ["id"] = offer.Id, ["request"] = request });
        Assert.False(result.Executed);
        Assert.IsType<ForbidResult>(result.Context.Result);
    }

    [Fact]
    public async Task Filter_rechecks_read_access_if_another_shipowner_was_selected_during_the_request()
    {
        var store = new DemoStore();
        var offer = store.Snapshot().Offers.Single(item => item.Reference == 2941);
        offer.Stage = OfferStage.Negotiation;
        offer.SelectedShipownerId = 120;
        var result = await RunFilter(store, User("Shipowner", 119), nameof(OffersController.Get),
            new() { ["id"] = offer.Id }, new OkObjectResult(offer));
        Assert.IsType<NotFoundResult>(result.ExecutedResult!.Result);
    }

    [Fact]
    public async Task Filter_redacts_dashboard_and_blocks_legacy_counter_endpoint()
    {
        var store = new DemoStore();
        var result = await RunFilter(store, User("Charterer", 40), nameof(OffersController.Dashboard), new(),
            new OffersController(store).Dashboard().Result!);
        var lanes = Assert.IsAssignableFrom<IReadOnlyCollection<DashboardLane>>(Assert.IsType<OkObjectResult>(result.ExecutedResult!.Result).Value);
        Assert.All(lanes, lane => Assert.Empty(lane.Offers));
        var legacy = await RunFilter(store, User("Charterer", 39), nameof(OffersController.Counter),
            new() { ["id"] = store.Offers.Single(item => item.Reference == 2942).Id });
        Assert.Equal(410, Assert.IsType<StatusCodeResult>(legacy.Context.Result).StatusCode);
    }

    [Fact]
    public void Demo_sessions_are_random_expiring_and_revocable()
    {
        var clock = new TestClock();
        var sessions = new DemoSessionStore(clock);
        var first = sessions.Create("charterer")!;
        var second = sessions.Create("charterer")!;
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(64, first.AccessToken.Length);
        Assert.Null(sessions.Find("forged"));
        Assert.Null(sessions.Create("unknown"));
        sessions.Revoke(first.AccessToken);
        Assert.Null(sessions.Find(first.AccessToken));
        clock.Now = clock.Now.AddHours(9);
        Assert.Null(sessions.Find(second.AccessToken));
    }

    [Theory]
    [InlineData("Development", true, "127.0.0.1", null, true)]
    [InlineData("Development", true, "::1", "http://localhost:4200", true)]
    [InlineData("Production", true, "127.0.0.1", null, false)]
    [InlineData("Development", false, "127.0.0.1", null, false)]
    [InlineData("Development", true, "192.168.1.20", null, false)]
    [InlineData("Development", true, "127.0.0.1", "https://untrusted.example", false)]
    public void Demo_access_requires_local_development_and_trusted_origin(string environment, bool enabled, string ip, string? origin, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (origin is not null) context.Request.Headers.Origin = origin;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Authentication:EnableLocalDemoSessions"] = enabled.ToString() }).Build();
        Assert.Equal(expected, LocalDemoAccess.Allowed(context, new TestEnvironment { EnvironmentName = environment }, config));
    }

    private static async Task<FilterResult> RunFilter(DemoStore store, ClaimsPrincipal user, string action,
        Dictionary<string, object?> arguments, IActionResult? returned = null)
    {
        var actionContext = new ActionContext(new DefaultHttpContext { User = user }, new RouteData(), new ControllerActionDescriptor { ActionName = action });
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), arguments, new object());
        var result = new FilterResult(context);
        await new OfferAccessFilter(store).OnActionExecutionAsync(context, () =>
        {
            result.Executed = true;
            result.ExecutedResult = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object())
            { Result = returned ?? new OkResult() };
            return Task.FromResult(result.ExecutedResult);
        });
        return result;
    }

    private sealed class FilterResult(ActionExecutingContext context)
    { public ActionExecutingContext Context { get; } = context; public bool Executed { get; set; } public ActionExecutedContext? ExecutedResult { get; set; } }
    private sealed class TestClock : TimeProvider
    { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}

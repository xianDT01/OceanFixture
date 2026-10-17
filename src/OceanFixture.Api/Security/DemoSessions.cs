using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OceanFixture.Api.Security;

public sealed record DemoAccount(string Id, string Name, string Role, int? CompanyId, string CompanyName);
public sealed record DemoSession(string AccessToken, DemoAccount User, DateTimeOffset ExpiresAt);

// Deliberately not a production identity provider. Available only in local Development.
public sealed class DemoSessionStore(TimeProvider clock)
{
    public static IReadOnlyList<DemoAccount> Accounts { get; } = Array.AsReadOnly(new[]
    {
        new DemoAccount("charterer", "Demo charterer", "Charterer", 39, "Charterer One"),
        new DemoAccount("charterer-two", "Demo second charterer", "Charterer", 40, "Charterer Two"),
        new DemoAccount("shipowner", "Demo shipowner", "Shipowner", 119, "Northstar Shipping"),
        new DemoAccount("shipowner-two", "Demo second shipowner", "Shipowner", 120, "Baltic Meridian"),
        new DemoAccount("admin", "Demo administrator", "Admin", null, "OceanFixture administration")
    });
    private readonly ConcurrentDictionary<string, DemoSession> sessions = new();

    public DemoSession? Create(string accountId)
    {
        var account = Accounts.FirstOrDefault(item => item.Id == accountId);
        if (account is null) return null;
        var session = new DemoSession(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), account, clock.GetUtcNow().AddHours(8));
        sessions[session.AccessToken] = session;
        return session;
    }

    public DemoSession? Find(string token)
    {
        if (!sessions.TryGetValue(token, out var session)) return null;
        if (session.ExpiresAt > clock.GetUtcNow()) return session;
        sessions.TryRemove(token, out _);
        return null;
    }
    public void Revoke(string token) => sessions.TryRemove(token, out _);
    public static string Token(HttpRequest request) => request.Headers.Authorization.ToString() is var value
        && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : "";
}

public static class LocalDemoAccess
{
    public static bool Allowed(HttpContext context, IWebHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:EnableLocalDemoSessions")
        && context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address)
        && (context.Request.Headers.Origin.Count == 0 || context.Request.Headers.Origin.ToString() == "http://localhost:4200");
}

public sealed class DemoBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, DemoSessionStore sessions, IWebHostEnvironment environment, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!LocalDemoAccess.Allowed(Context, environment, configuration))
            return Task.FromResult(AuthenticateResult.NoResult());
        var token = DemoSessionStore.Token(Request);
        if (token.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var session = sessions.Find(token);
        if (session is null) return Task.FromResult(AuthenticateResult.Fail("Invalid or expired demo session."));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id), new(ClaimTypes.Name, session.User.Name),
            new(ClaimTypes.Role, session.User.Role)
        };
        if (session.User.CompanyId is { } companyId) claims.Add(new("company_id", companyId.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

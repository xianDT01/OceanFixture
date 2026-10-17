using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Security;
using OceanFixture.Api.Data;

namespace OceanFixture.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("v1/api/demo-session")]
public sealed class DemoSessionController(DemoSessionStore sessions, IWebHostEnvironment environment, IConfiguration configuration, DemoStore store) : ControllerBase
{
    [HttpGet("accounts")]
    public IActionResult Accounts() => LocalDemoAccess.Allowed(HttpContext, environment, configuration)
        ? Ok(AvailableAccounts()) : NotFound();

    [HttpPost("login")]
    public IActionResult Login(DemoLoginRequest request)
    {
        if (!LocalDemoAccess.Allowed(HttpContext, environment, configuration)) return NotFound();
        var session = AvailableAccounts().Any(account => account.Id == request.AccountId) ? sessions.Create(request.AccountId) : null;
        if (session is null) return BadRequest(new { message = "Unknown demo account." });
        sessions.Revoke(DemoSessionStore.Token(Request));
        return Ok(session);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => sessions.Find(DemoSessionStore.Token(Request)) is { } session ? Ok(session.User) : Unauthorized();

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() { sessions.Revoke(DemoSessionStore.Token(Request)); return NoContent(); }
    private IEnumerable<DemoAccount> AvailableAccounts()
    {
        var companies = store.Snapshot().Companies;
        return DemoSessionStore.Accounts.Where(account => account.Role == "Admin" || companies.Any(company =>
            company.Id == account.CompanyId && company.Type == account.Role && company.Approved));
    }
}

public sealed record DemoLoginRequest(string AccountId);

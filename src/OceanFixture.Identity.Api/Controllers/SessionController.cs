using Microsoft.AspNetCore.Mvc;

namespace OceanFixture.Identity.Api.Controllers;

[ApiController]
[Route("v1/api/session")]
public sealed class SessionController : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        return Ok(new LoginResponse(
            Guid.NewGuid().ToString("N"),
            request.Email,
            ["CLH", "Charterer", "Shipowner"],
            DateTimeOffset.UtcNow.AddHours(8)));
    }

    [HttpGet("permissions")]
    public ActionResult<string[]> Permissions() => Ok(
        new[] { "offers.read", "offers.write", "recap.confirm", "operations.read", "companies.read" });
}

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, string Email, string[] Roles, DateTimeOffset ExpiresAt);

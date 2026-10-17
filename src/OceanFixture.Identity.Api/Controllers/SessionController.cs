using Microsoft.AspNetCore.Mvc;

namespace OceanFixture.Identity.Api.Controllers;

[ApiController]
[Route("v1/api/session")]
public sealed class SessionController : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        return StatusCode(501, new { message = "Production identity is not implemented. Use the local Development demo profiles in OceanFixture.Api." });
    }

    [HttpGet("permissions")]
    public ActionResult<string[]> Permissions() => StatusCode(501, new { message = "Permissions are enforced by OceanFixture.Api; this legacy placeholder is disabled." });
}

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, string Email, string[] Roles, DateTimeOffset ExpiresAt);

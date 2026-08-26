using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Domain;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Route("v1/api/companies")]
public sealed class CompaniesController(DemoStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<Company>> Get([FromQuery] string? type = null) => Ok(
        string.IsNullOrWhiteSpace(type)
            ? store.Companies
            : store.Companies.Where(company => company.Type.Equals(type, StringComparison.OrdinalIgnoreCase)));
}

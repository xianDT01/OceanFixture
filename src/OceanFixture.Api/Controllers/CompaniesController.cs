using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Domain;
using Microsoft.AspNetCore.Authorization;
using OceanFixture.Api.Security;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Authorize(Roles = "Charterer,Shipowner,Admin")]
[Route("v1/api/companies")]
public sealed class CompaniesController(DemoStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<Company>> Get([FromQuery] string? type = null)
    {
        var companies = store.Snapshot().Companies;
        var access = new OfferAccess(User);
        companies = companies.Where(company => access.IsAdmin || company.Id == access.CompanyId
            || (access.IsCharterer && company.Type == "Shipowner" && company.Approved)).ToList();
        return Ok(string.IsNullOrWhiteSpace(type) ? companies
            : companies.Where(company => company.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList());
    }
}

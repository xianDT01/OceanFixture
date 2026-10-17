using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Authorize(Roles = "Charterer,Shipowner")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("v1/api/offers/{id:guid}/negotiation/fields/{field}/acceptance")]
public sealed class FieldAcceptancesController(DemoStore store) : ControllerBase
{
    [HttpPut]
    public ActionResult<CargoOffer> Set(Guid id, string field, SetFieldAcceptanceRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            var access = new OfferAccess(User);
            if (offer is null || !(access.OwnsCargo(offer) || access.IsSelectedOwner(offer))) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Forbid();
            if (offer.Stage != OfferStage.Negotiation) return BadRequest(new { message = "The offer is not in negotiation." });
            if (request.VersionHash != offer.RecapVersion) return Conflict(new { message = "Conditions have changed. Reload before accepting." });
            var hash = NegotiationFields.ValueHash(offer.Terms, field);
            if (hash is null) return BadRequest(new { message = "Unknown negotiation field." });
            var previous = offer.FieldAcceptances.FirstOrDefault(item => item.Field == field && item.Party == access.Role);
            // Idempotent repeats preserve the original author and timestamp.
            if (request.Accepted ? previous?.ValueHash != hash : previous is not null)
                store.Commit(() =>
                {
                    offer.FieldAcceptances.RemoveAll(item => item.Field == field && item.Party == access.Role);
                    if (request.Accepted) offer.FieldAcceptances.Add(new(field, access.Role!, userId, DateTimeOffset.UtcNow, hash));
                });
            return Ok(access.Project(store.Snapshot().Offers.Single(item => item.Id == id)));
        }
    }
}

public sealed record SetFieldAcceptanceRequest(string VersionHash, bool Accepted);

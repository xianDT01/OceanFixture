using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Api.Security;
using OceanFixture.Domain;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Authorize(Roles = "Charterer,Shipowner")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("v1/api/offers/{id:guid}/negotiation/draft")]
public sealed class NegotiationDraftsController(DemoStore store) : ControllerBase
{
    private static readonly HashSet<string> Keys = typeof(VoyageTerms).GetProperties()
        .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).ToHashSet();

    [HttpGet]
    public ActionResult<NegotiationDraft?> Get(Guid id)
    {
        lock (store)
        {
            var offer = store.Snapshot().Offers.FirstOrDefault(item => item.Id == id);
            if (!CanAccess(offer)) return NotFound();
            return Ok(offer!.NegotiationDrafts.FirstOrDefault(item => item.UserId == UserId));
        }
    }

    [HttpPut]
    public ActionResult<NegotiationDraft> Save(Guid id, SaveNegotiationDraftRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (!CanAccess(offer)) return NotFound();
            if (offer!.Stage != OfferStage.Negotiation) return BadRequest(new { message = "Negotiation is closed." });
            if (request.BaseVersion != offer.RecapVersion) return Conflict(new { message = "The negotiation has changed. Reload before saving." });
            if (!new OfferAccess(User).Permissions(offer).CanSendCounter) return Forbid();
            // Strings deliberately allow unfinished numeric fields, without applying them to the offer.
            if (request.Values is null || request.OwnClauses is null || request.Values.Count > Keys.Count
                || request.Values.Any(item => !Keys.Contains(item.Key) || item.Value is null || item.Value.Length > 10000)
                || request.OwnClauses.Length > 200 || request.OwnClauses.Any(item => item is null || item.Length > 10000))
                return BadRequest(new { message = "Invalid draft fields or clauses." });
            var draft = new NegotiationDraft(UserId!, new OfferAccess(User).Role!, request.BaseVersion,
                DateTimeOffset.UtcNow, new(request.Values), request.OwnClauses.ToArray());
            store.Commit(() =>
            {
                offer.NegotiationDrafts.RemoveAll(item => item.UserId == UserId);
                offer.NegotiationDrafts.Add(draft);
            });
            return Ok(store.Snapshot().Offers.Single(item => item.Id == id).NegotiationDrafts.Single(item => item.UserId == UserId));
        }
    }

    [HttpDelete]
    public IActionResult Delete(Guid id)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (!CanAccess(offer)) return NotFound();
            if (offer!.NegotiationDrafts.Any(item => item.UserId == UserId))
                store.Commit(() => offer.NegotiationDrafts.RemoveAll(item => item.UserId == UserId));
            return NoContent();
        }
    }

    private string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool CanAccess(CargoOffer? offer) => offer is not null && !string.IsNullOrWhiteSpace(UserId)
        && (new OfferAccess(User).OwnsCargo(offer) || new OfferAccess(User).IsSelectedOwner(offer));
}

public sealed record SaveNegotiationDraftRequest(string BaseVersion, Dictionary<string, string> Values, string[] OwnClauses);

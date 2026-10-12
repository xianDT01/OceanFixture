using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Domain;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Route("v1/api/offers")]
public sealed class OffersController(DemoStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<CargoOffer>> GetAll() => Ok(store.Offers);

    [HttpGet("dashboard")]
    public ActionResult<IReadOnlyCollection<DashboardLane>> Dashboard() => Ok(
        Enum.GetValues<OfferStage>().Select(stage => new DashboardLane(
            stage.ToString(),
            stage switch
            {
                OfferStage.CargoOrder => "CARGO ORDERS",
                OfferStage.Negotiation => "NEGOTIATION",
                OfferStage.Operative => "OPERATIVE",
                _ => "ARCHIVE"
            },
            store.Offers.Where(offer => offer.Stage == stage).ToArray())).ToArray());

    [HttpGet("{id:guid}")]
    public ActionResult<CargoOffer> Get(Guid id) =>
        store.Offers.FirstOrDefault(offer => offer.Id == id) is { } offer ? Ok(offer) : NotFound();

    [HttpGet("{reference:int}")]
    public ActionResult<CargoOffer> GetByReference(int reference) =>
        store.Offers.FirstOrDefault(offer => offer.Reference == reference) is { } offer ? Ok(offer) : NotFound();

    [HttpPost]
    public ActionResult<CargoOffer> Create(CargoOffer offer)
    {
        var created = new CargoOffer
        {
            Reference = store.Offers.Max(item => item.Reference) + 1,
            CustomerReference = offer.CustomerReference,
            Terms = offer.Terms,
            InvitedShipownerIds = offer.InvitedShipownerIds
        };
        store.Offers.Add(created);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/firm-offer/{shipownerId:int}")]
    public ActionResult<CargoOffer> SelectFirmOffer(Guid id, int shipownerId)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            var bid = offer?.Bids.FirstOrDefault(item => item.ShipownerId == shipownerId);
            if (offer is null || bid is null) return NotFound();
            offer.SelectedShipownerId = shipownerId;
            offer.Terms = bid.Terms;
            offer.Confirmations.Clear();
            offer.Stage = OfferStage.Negotiation;
            offer.NegotiationTurn = "Shipowner";
            return Ok(offer);
        }
    }

    [HttpPost("{id:guid}/counter")]
    public ActionResult<CargoOffer> Counter(Guid id, VoyageTerms terms)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (offer is null) return NotFound();
            offer.Terms = terms;
            offer.Confirmations.Clear();
            offer.Stage = OfferStage.Negotiation;
            offer.Counter++;
            offer.NegotiationTurn = offer.NegotiationTurn == "Charterer" ? "Shipowner" : "Charterer";
            return Ok(offer);
        }
    }

    [HttpPost("{id:guid}/recap/confirm")]
    public ActionResult<CargoOffer> ConfirmRecap(Guid id, ConfirmRecapRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (offer is null) return NotFound();
            if (offer.Stage is not (OfferStage.Negotiation or OfferStage.Operative))
                return BadRequest(new { message = "The offer is not ready for recap confirmation." });
            var party = request.Party?.Trim().ToLowerInvariant();
            if (party is not ("charterer" or "shipowner") || string.IsNullOrWhiteSpace(request.ConfirmedBy))
                return BadRequest(new { message = "A valid party and confirming user are required." });
            if (request.VersionHash != offer.RecapVersion)
                return Conflict(new { message = "The recap has changed. Reload before confirming." });
            offer.Confirmations.RemoveAll(item => item.Party.Equals(party, StringComparison.OrdinalIgnoreCase));
            offer.Confirmations.Add(new(party, request.ConfirmedBy.Trim(), DateTimeOffset.UtcNow, offer.RecapVersion));
            if (offer.Confirmations.Where(item => item.VersionHash == offer.RecapVersion)
                .Select(item => item.Party.ToLowerInvariant()).Distinct().Count() == 2)
                offer.Stage = OfferStage.Operative;
            return Ok(offer);
        }
    }
}

public sealed record ConfirmRecapRequest(string Party, string ConfirmedBy, string VersionHash);

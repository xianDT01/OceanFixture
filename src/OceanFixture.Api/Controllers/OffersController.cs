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
        var offer = store.Offers.FirstOrDefault(item => item.Id == id);
        var bid = offer?.Bids.FirstOrDefault(item => item.ShipownerId == shipownerId);
        if (offer is null || bid is null) return NotFound();
        offer.SelectedShipownerId = shipownerId;
        offer.Terms = bid.Terms;
        offer.Stage = OfferStage.Negotiation;
        offer.NegotiationTurn = "Shipowner";
        return Ok(offer);
    }

    [HttpPost("{id:guid}/counter")]
    public ActionResult<CargoOffer> Counter(Guid id, VoyageTerms terms)
    {
        var offer = store.Offers.FirstOrDefault(item => item.Id == id);
        if (offer is null) return NotFound();
        offer.Terms = terms;
        offer.Counter++;
        offer.NegotiationTurn = offer.NegotiationTurn == "Charterer" ? "Shipowner" : "Charterer";
        return Ok(offer);
    }

    [HttpPost("{id:guid}/recap/confirm")]
    public ActionResult<CargoOffer> ConfirmRecap(Guid id, ConfirmRecapRequest request)
    {
        var offer = store.Offers.FirstOrDefault(item => item.Id == id);
        if (offer is null) return NotFound();
        offer.Confirmations.RemoveAll(item => item.Party.Equals(request.Party, StringComparison.OrdinalIgnoreCase));
        offer.Confirmations.Add(new(request.Party, request.ConfirmedBy, DateTimeOffset.UtcNow, request.VersionHash));
        if (offer.Confirmations.Select(item => item.Party.ToLowerInvariant()).Distinct().Count() >= 2)
            offer.Stage = OfferStage.Operative;
        return Ok(offer);
    }
}

public sealed record ConfirmRecapRequest(string Party, string ConfirmedBy, string VersionHash);

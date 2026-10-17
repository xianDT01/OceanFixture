using Microsoft.AspNetCore.Mvc;
using OceanFixture.Api.Data;
using OceanFixture.Domain;
using Microsoft.AspNetCore.Authorization;
using OceanFixture.Api.Security;

namespace OceanFixture.Api.Controllers;

[ApiController]
[Authorize(Roles = "Charterer,Shipowner,Admin")]
[ServiceFilter(typeof(OfferAccessFilter))]
[Route("v1/api/offers")]
public sealed class OffersController(DemoStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<CargoOffer>> GetAll() => Ok(store.Snapshot().Offers);

    [HttpGet("dashboard")]
    public ActionResult<IReadOnlyCollection<DashboardLane>> Dashboard()
    {
        var offers = store.Snapshot().Offers;
        return Ok(
        Enum.GetValues<OfferStage>().Select(stage => new DashboardLane(
            stage.ToString(),
            stage switch
            {
                OfferStage.CargoOrder => "CARGO ORDERS",
                OfferStage.Negotiation => "NEGOTIATION",
                OfferStage.Operative => "OPERATIVE",
                _ => "ARCHIVE"
            },
            offers.Where(offer => offer.Stage == stage).ToArray())).ToArray());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<CargoOffer> Get(Guid id) =>
        store.Snapshot().Offers.FirstOrDefault(offer => offer.Id == id) is { } offer ? Ok(offer) : NotFound();

    [HttpGet("{reference:int}")]
    public ActionResult<CargoOffer> GetByReference(int reference) =>
        store.Snapshot().Offers.FirstOrDefault(offer => offer.Reference == reference) is { } offer ? Ok(offer) : NotFound();

    [HttpPost]
    public ActionResult<CargoOffer> Create(CreateCargoOfferRequest request)
    {
        lock (store)
        {
            var terms = request.Terms;
            if (string.IsNullOrWhiteSpace(request.CustomerReference) || terms is null
                || string.IsNullOrWhiteSpace(terms.Charterer) || string.IsNullOrWhiteSpace(terms.Commodity)
                || string.IsNullOrWhiteSpace(terms.PortOfLoading) || string.IsNullOrWhiteSpace(terms.PortOfDischarge)
                || string.IsNullOrWhiteSpace(terms.Laycan) || string.IsNullOrWhiteSpace(terms.QuantityUnit)
                || terms.Quantity <= 0 || terms.Freight < 0 || terms.Demurrage < 0)
                return BadRequest(new { message = "Reference, charterer, cargo, positive quantity, ports and laycan are required. Amounts cannot be negative." });
            if (request.InvitedShipownerIds is null || request.InvitedShipownerIds.Length == 0
                || request.InvitedShipownerIds.Any(id => !store.Companies.Any(company =>
                    company.Id == id && company.Type == "Shipowner" && company.Approved)))
                return BadRequest(new { message = "Select at least one approved shipowner from the company catalog." });
            var created = new CargoOffer
            {
                Reference = store.Offers.Select(item => item.Reference).DefaultIfEmpty(0).Max() + 1,
                CustomerReference = request.CustomerReference.Trim(),
                ChartererCompanyId = request.ChartererCompanyId ?? 39,
                Terms = terms,
                InvitedShipownerIds = request.InvitedShipownerIds.Distinct().ToList()
            };
            store.Commit(() => store.Offers.Add(created));
            return CreatedAtAction(nameof(Get), new { id = created.Id }, SnapshotOffer(created));
        }
    }

    [HttpPost("{id:guid}/bids")]
    public ActionResult<CargoOffer> SubmitBid(Guid id, SubmitBidRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (offer is null) return NotFound();
            if (offer.Stage != OfferStage.CargoOrder)
                return BadRequest(new { message = "Offers can only be submitted while the cargo order is open." });
            if (request.VersionHash != offer.RecapVersion)
                return Conflict(new { message = "The cargo order has changed. Reload before submitting." });
            var company = store.Companies.FirstOrDefault(item => item.Id == request.ShipownerId && item.Type == "Shipowner" && item.Approved);
            if (company is null || !offer.InvitedShipownerIds.Contains(company.Id)) return Forbid();
            var terms = request.Terms;
            if (terms is null || terms.Quantity <= 0 || terms.Freight is null or <= 0 || terms.Demurrage < 0
                || string.IsNullOrWhiteSpace(terms.Commodity) || string.IsNullOrWhiteSpace(terms.PortOfLoading)
                || string.IsNullOrWhiteSpace(terms.PortOfDischarge) || string.IsNullOrWhiteSpace(terms.Laycan)
                || string.IsNullOrWhiteSpace(terms.QuantityUnit) || string.IsNullOrWhiteSpace(terms.FreightUnit)
                || string.IsNullOrWhiteSpace(terms.FreightCurrency) || terms.FreightCurrency.Length != 3
                || !terms.FreightCurrency.All(char.IsAsciiLetter))
                return BadRequest(new { message = "Valid cargo terms, a positive freight, currency and units are required." });
            var previous = offer.Bids.FirstOrDefault(bid => bid.ShipownerId == company.Id);
            var previousIndex = offer.Bids.FindIndex(item => item.ShipownerId == company.Id);
            var bid = new ShipownerBid(previous?.Id ?? Guid.NewGuid(), company.Id, company.Name, DateTimeOffset.UtcNow,
                terms with { Charterer = offer.Terms.Charterer, FreightCurrency = terms.FreightCurrency.ToUpperInvariant() });
            store.Commit(() =>
            {
                offer.Bids.RemoveAll(item => item.ShipownerId == company.Id);
                offer.Bids.Insert(previousIndex < 0 ? offer.Bids.Count : Math.Min(previousIndex, offer.Bids.Count), bid);
            });
            return Ok(SnapshotOffer(offer));
        }
    }

    [HttpPost("{id:guid}/firm-offer/{shipownerId:int}")]
    public ActionResult<CargoOffer> SelectFirmOffer(Guid id, int shipownerId, SelectFirmOfferRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            var bid = offer?.Bids.FirstOrDefault(item => item.ShipownerId == shipownerId);
            if (offer is null || bid is null) return NotFound();
            if (request.VersionHash != offer.RecapVersion)
                return Conflict(new { message = "The offer has changed. Reload before selecting." });
            if (offer.Stage != OfferStage.CargoOrder)
                return BadRequest(new { message = "A firm offer can only be selected for a cargo order." });
            store.Commit(() =>
            {
                offer.SelectedShipownerId = shipownerId;
                offer.Terms = bid.Terms;
                offer.Confirmations.Clear();
                offer.Stage = OfferStage.Negotiation;
                offer.NegotiationTurn = "Shipowner";
            });
            return Ok(SnapshotOffer(offer));
        }
    }

    [HttpPost("{id:guid}/counter")]
    public ActionResult<CargoOffer> Counter(Guid id, VoyageTerms terms)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (offer is null) return NotFound();
            store.Commit(() => ApplyCounter(offer, terms));
            return Ok(SnapshotOffer(offer));
        }
    }

    [HttpPost("{id:guid}/negotiation/counter")]
    public ActionResult<CargoOffer> SendNegotiatedCounter(Guid id, NegotiatedCounterRequest request)
    {
        lock (store)
        {
            var offer = store.Offers.FirstOrDefault(item => item.Id == id);
            if (offer is null) return NotFound();
            if (offer.Stage != OfferStage.Negotiation) return BadRequest(new { message = "The offer is not in negotiation." });
            if (request.VersionHash != offer.RecapVersion) return Conflict(new { message = "The offer has changed. Reload before sending." });
            // Recheck under the same lock as the write, not just in the access filter.
            if (request.Party is not ("Charterer" or "Shipowner") || string.IsNullOrWhiteSpace(request.SubmittedBy)) return Forbid();
            if (request.Party != offer.NegotiationTurn)
                return Conflict(new { message = "It is the other party's turn. Reload before sending." });
            if (request.Terms is null || request.Terms.Quantity <= 0 || request.Terms.Freight < 0 || request.Terms.Demurrage < 0
                || request.ChartererClauses is null || request.ShipownerClauses is null)
                return BadRequest(new { message = "Valid terms and clauses are required." });
            store.Commit(() =>
            {
                var before = new NegotiationSnapshot(offer.Terms, offer.ChartererClauses.ToArray(), offer.ShipownerClauses.ToArray());
                offer.ChartererClauses.Clear();
                offer.ChartererClauses.AddRange(request.ChartererClauses);
                offer.ShipownerClauses.Clear();
                offer.ShipownerClauses.AddRange(request.ShipownerClauses);
                ApplyCounter(offer, request.Terms);
                offer.NegotiationDrafts.RemoveAll(draft => draft.UserId == request.SubmittedBy);
                offer.NegotiationHistory.Add(new(Guid.NewGuid(), offer.Counter, request.Party, request.SubmittedBy,
                    DateTimeOffset.UtcNow, before,
                    new(offer.Terms, offer.ChartererClauses.ToArray(), offer.ShipownerClauses.ToArray())));
            });
            return Ok(SnapshotOffer(offer));
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
            store.Commit(() =>
            {
                offer.Confirmations.RemoveAll(item => item.Party.Equals(party, StringComparison.OrdinalIgnoreCase));
                offer.Confirmations.Add(new(party, request.ConfirmedBy.Trim(), DateTimeOffset.UtcNow, offer.RecapVersion));
                if (offer.Confirmations.Where(item => item.VersionHash == offer.RecapVersion)
                    .Select(item => item.Party.ToLowerInvariant()).Distinct().Count() == 2)
                    offer.Stage = OfferStage.Operative;
            });
            return Ok(SnapshotOffer(offer));
        }
    }
    private CargoOffer SnapshotOffer(CargoOffer offer) => store.Snapshot().Offers.Single(item => item.Id == offer.Id);

    private static void ApplyCounter(CargoOffer offer, VoyageTerms terms)
    {
        offer.Terms = terms;
        offer.Confirmations.Clear();
        offer.Stage = OfferStage.Negotiation;
        offer.Counter++;
        offer.NegotiationTurn = offer.NegotiationTurn == "Charterer" ? "Shipowner" : "Charterer";
    }
}

public sealed record ConfirmRecapRequest(string Party, string? ConfirmedBy, string VersionHash);
public sealed record CreateCargoOfferRequest(string CustomerReference, VoyageTerms Terms, int[] InvitedShipownerIds, int? ChartererCompanyId = null);
public sealed record SelectFirmOfferRequest(string VersionHash);
public sealed record NegotiatedCounterRequest(VoyageTerms Terms, string[] ChartererClauses, string[] ShipownerClauses, string VersionHash)
{
    // These values are assigned by the access filter, never accepted from JSON.
    [System.Text.Json.Serialization.JsonIgnore] public string? Party { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public string? SubmittedBy { get; init; }
}
public sealed record SubmitBidRequest(VoyageTerms Terms, string VersionHash, int? ShipownerId = null);

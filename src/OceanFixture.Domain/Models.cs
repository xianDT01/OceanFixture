namespace OceanFixture.Domain;

public enum OfferStage
{
    CargoOrder,
    Negotiation,
    Operative,
    Archive
}

public sealed record Company(
    int Id,
    string Name,
    string Type,
    string Country,
    string Vat,
    bool Approved = true);

public sealed record VoyageTerms
{
    public string Charterer { get; init; } = "";
    public decimal Quantity { get; init; }
    public string QuantityUnit { get; init; } = "MT";
    public string StowageFactor { get; init; } = "NA";
    public string Commodity { get; init; } = "";
    public string PortOfLoading { get; init; } = "";
    public string LoadingConditions { get; init; } = "";
    public string PortOfDischarge { get; init; } = "";
    public string DischargeConditions { get; init; } = "";
    public string Laycan { get; init; } = "";
    public string LoadingDischargingTerms { get; init; } = "";
    public string LaytimeTerms { get; init; } = "";
    public string CharterPartyForm { get; init; } = "GENCON 94";
    public string Restrictions { get; init; } = "";
    public string AdditionalRequirements { get; init; } = "";
    public decimal? Freight { get; init; }
    public string FreightCurrency { get; init; } = "USD";
    public string FreightUnit { get; init; } = "LPSM";
    public decimal? Demurrage { get; init; }
    public string DemurrageUnit { get; init; } = "PDPR";
    public string Despatch { get; init; } = "";
    public string VesselName { get; init; } = "";
    public string Remarks { get; init; } = "";
}

public sealed record ShipownerBid(
    Guid Id,
    int ShipownerId,
    string ShipownerName,
    DateTimeOffset ReceivedAt,
    VoyageTerms Terms);

public sealed record RecapConfirmation(
    string Party,
    string ConfirmedBy,
    DateTimeOffset ConfirmedAt,
    string VersionHash);

public sealed class CargoOffer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Reference { get; init; }
    public string CustomerReference { get; set; } = "";
    public DateOnly CreatedOn { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public OfferStage Stage { get; set; } = OfferStage.CargoOrder;
    public VoyageTerms Terms { get; set; } = new();
    public List<int> InvitedShipownerIds { get; init; } = [];
    public List<ShipownerBid> Bids { get; init; } = [];
    public int? SelectedShipownerId { get; set; }
    public string NegotiationTurn { get; set; } = "Charterer";
    public int Counter { get; set; }
    public List<string> ChartererClauses { get; init; } = [];
    public List<string> ShipownerClauses { get; init; } = [];
    public List<RecapConfirmation> Confirmations { get; init; } = [];
}

public sealed record DashboardLane(string Key, string Label, IReadOnlyCollection<CargoOffer> Offers);

public sealed record MessageTemplate(string Key, string Description, string Subject);

public sealed record StoredFile(Guid Id, string Name, string ContentType, long Size, DateTimeOffset UploadedAt);

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

public sealed record NegotiationSnapshot(VoyageTerms Terms, string[] ChartererClauses, string[] ShipownerClauses);
public sealed record NegotiationEntry(Guid Id, int Counter, string Party, string SubmittedBy,
    DateTimeOffset SubmittedAt, NegotiationSnapshot Before, NegotiationSnapshot After);
public sealed record NegotiationDraft(string UserId, string Party, string BaseVersion, DateTimeOffset SavedAt,
    Dictionary<string, string> Values, string[] OwnClauses);
public sealed record FieldAcceptance(string Field, string Party, string AcceptedBy, DateTimeOffset AcceptedAt, string ValueHash);

public sealed class CargoOffer
{
    // Existing local demo snapshots belong to Charterer One (39).
    public int ChartererCompanyId { get; init; } = 39;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public OfferPermissions? Permissions { get; set; }
    private string? projectedRecapVersion;
    // A read-only redacted response must retain the version of the full server state.
    public void PreserveVersionForReadOnlyProjection() => projectedRecapVersion = RecapVersion;
    public string RecapVersion => projectedRecapVersion ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            Reference,
            CustomerReference,
            Terms,
            SelectedShipownerId,
            ChartererClauses,
            ShipownerClauses,
            Counter,
            NegotiationTurn,
            Bids
        })));
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
    // Audit data is deliberately excluded from RecapVersion to preserve existing signatures.
    public List<NegotiationEntry> NegotiationHistory { get; init; } = [];
    public List<NegotiationDraft> NegotiationDrafts { get; init; } = [];
    public List<FieldAcceptance> FieldAcceptances { get; init; } = [];
}

public sealed record DashboardLane(string Key, string Label, IReadOnlyCollection<CargoOffer> Offers);
public sealed record OfferPermissions(bool CanSelectFirmOffer, bool CanSendCounter, bool CanConfirmAsCharterer,
    bool CanConfirmAsShipowner, bool CanEditChartererClauses, bool CanEditShipownerClauses, bool CanSubmitBid = false);

public sealed record MessageTemplate(string Key, string Description, string Subject);

public sealed record StoredFile(Guid Id, string Name, string ContentType, long Size, DateTimeOffset UploadedAt);

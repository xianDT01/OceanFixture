using OceanFixture.Domain;

namespace OceanFixture.Api.Data;

public sealed class DemoStore
{
    public List<Company> Companies { get; } =
    [
        new(39, "Charterer One", "Charterer", "Spain", "A82303769"),
        new(119, "Northstar Shipping", "Shipowner", "Germany", "A82303768"),
        new(120, "Baltic Meridian", "Shipowner", "Lithuania", "A82303767"),
        new(121, "Bluehaven Carriers", "Shipowner", "Spain", "A82303765"),
        new(122, "Atlantic Crest", "Shipowner", "Spain", "A82303764")
    ];

    public List<CargoOffer> Offers { get; }

    public DemoStore()
    {
        Offers =
        [
            CreateOffer(2957, "AUTOMATED TEST CARGO", OfferStage.CargoOrder, 5_000, "STEEL COILS", "", "", "", "2025-08-19"),
            CreateOffer(2943, "ALT-BIO25-25", OfferStage.CargoOrder, 8_500, "Iron ore", "Cartagena (COCTG)", "Gijon (ESGIJ)", "06/07/25 - 10/07/25", "2025-06-17"),
            CreateOffer(2940, "ALTBIO 441-25", OfferStage.CargoOrder, 12_000, "Seamless steel pipes", "Tianjin Xingang (CNTXG)", "Bejaia (DZBJA)", "23/02/25 - 27/02/25", "2025-02-03"),
            CreateOffer(2942, "ALTBIO 331-25", OfferStage.Negotiation, 4_000, "High-strength steel plates", "La Spezia (ITSPE)", "Vigo (ESVGO)", "16/02/25 - 19/02/25", "2025-03-03"),
            CreateOffer(2939, "ALTBIO - 225-25", OfferStage.Operative, 21_000, "Steel plates as per attached packing list", "Gdansk (PLGDN)", "Casablanca (MACAS)", "10/02/25 - 14/02/25", "2025-01-29"),
            CreateOffer(2937, "ALTBIO-176-25", OfferStage.Archive, 7_500, "Bulk steam coal", "Cartagena (COCTG)", "Aviles (ESAVS)", "02/02/25 - 06/02/25", "2025-01-24"),
            CreateOffer(2938, "ALTBIO-323-25", OfferStage.Archive, 6_800, "WOODEN BOXES AS PER ATTACHED", "Vigo (ESVGO)", "Immingham (GBIMM)", "01/02/25 - 12/02/25", "2025-01-29")
        ];

        var comparative = CreateOffer(2941, "ALT BIO 445-25", OfferStage.Operative, 9_000, "Cold-rolled steel coils", "Derince (TRDRC)", "Bilbao (ESBIO)", "18/02/25 - 20/02/25", "2025-02-03");
        comparative.Bids.AddRange(
        [
            Bid(120, "Baltic Meridian", comparative.Terms with { Freight = 195_000, FreightCurrency = "EUR", VesselName = "EVER-SKY" }),
            Bid(121, "Bluehaven Carriers", comparative.Terms with { Freight = 265_000, VesselName = "GREENSTONE", PortOfLoading = "Izmir (TRIZM)" }),
            Bid(119, "Northstar Shipping", comparative.Terms with { Freight = 220_000, FreightCurrency = "EUR", VesselName = "EVER-OCEAN" })
        ]);
        Offers.Add(comparative);
    }

    private static CargoOffer CreateOffer(int reference, string customerReference, OfferStage stage, decimal quantity, string commodity, string pol, string pod, string laycan, string createdOn) => new()
    {
        Reference = reference,
        CreatedOn = DateOnly.ParseExact(createdOn, "yyyy-MM-dd"),
        CustomerReference = customerReference,
        Stage = stage,
        Terms = new VoyageTerms
        {
            Charterer = "Charterer One",
            Quantity = quantity,
            Commodity = commodity,
            PortOfLoading = pol,
            PortOfDischarge = pod,
            Laycan = laycan,
            LoadingDischargingTerms = "FIOS",
            LaytimeTerms = "72H WWD",
            Restrictions = "MAX 24 YEARS VESSEL"
        },
        InvitedShipownerIds = [119, 120, 121]
    };

    private static ShipownerBid Bid(int ownerId, string name, VoyageTerms terms) =>
        new(Guid.NewGuid(), ownerId, name, DateTimeOffset.UtcNow, terms);
}

using System.Security.Cryptography;
using System.Text.Json;

namespace OceanFixture.Domain;

public static class NegotiationFields
{
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["charterer"] = [nameof(VoyageTerms.Charterer)],
        ["quantity"] = [nameof(VoyageTerms.Quantity), nameof(VoyageTerms.QuantityUnit)],
        ["stowageFactor"] = [nameof(VoyageTerms.StowageFactor)],
        ["commodity"] = [nameof(VoyageTerms.Commodity)],
        ["portOfLoading"] = [nameof(VoyageTerms.PortOfLoading)],
        ["loadingConditions"] = [nameof(VoyageTerms.LoadingConditions)],
        ["portOfDischarge"] = [nameof(VoyageTerms.PortOfDischarge)],
        ["dischargeConditions"] = [nameof(VoyageTerms.DischargeConditions)],
        ["laycan"] = [nameof(VoyageTerms.Laycan)],
        ["loadingDischargingTerms"] = [nameof(VoyageTerms.LoadingDischargingTerms)],
        ["laytimeTerms"] = [nameof(VoyageTerms.LaytimeTerms)],
        ["charterPartyForm"] = [nameof(VoyageTerms.CharterPartyForm)],
        ["restrictions"] = [nameof(VoyageTerms.Restrictions)],
        ["additionalRequirements"] = [nameof(VoyageTerms.AdditionalRequirements)],
        ["freight"] = [nameof(VoyageTerms.Freight), nameof(VoyageTerms.FreightCurrency), nameof(VoyageTerms.FreightUnit)],
        ["demurrage"] = [nameof(VoyageTerms.Demurrage), nameof(VoyageTerms.DemurrageUnit)],
        ["despatch"] = [nameof(VoyageTerms.Despatch)],
        ["vesselName"] = [nameof(VoyageTerms.VesselName)],
        ["remarks"] = [nameof(VoyageTerms.Remarks)]
    };

    public static string? ValueHash(VoyageTerms terms, string field) => Fields.TryGetValue(field, out var properties)
        ? Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            properties.Select(name => Normalize(typeof(VoyageTerms).GetProperty(name)!.GetValue(terms))).ToArray()))) : null;

    private static object? Normalize(object? value) => value is decimal amount
        ? amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) : value;

    public static void RemoveChangedAcceptances(CargoOffer offer) => offer.FieldAcceptances.RemoveAll(item =>
        ValueHash(offer.Terms, item.Field) is not { } hash || item.ValueHash != hash);
}

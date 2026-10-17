using System.Security.Claims;
using OceanFixture.Domain;

namespace OceanFixture.Api.Security;

public sealed class OfferAccess(ClaimsPrincipal user)
{
    public string? Role => user.FindFirstValue(ClaimTypes.Role);
    public int? CompanyId => int.TryParse(user.FindFirstValue("company_id"), out var id) ? id : null;
    public bool IsAdmin => user.Identity?.IsAuthenticated == true && Role == "Admin";
    public bool IsCharterer => user.Identity?.IsAuthenticated == true && Role == "Charterer" && CompanyId is > 0;
    public bool IsShipowner => user.Identity?.IsAuthenticated == true && Role == "Shipowner" && CompanyId is > 0;
    public bool CanCreate => IsAdmin || IsCharterer;
    public bool OwnsCargo(CargoOffer offer) => IsCharterer && offer.ChartererCompanyId == CompanyId;
    public bool IsSelectedOwner(CargoOffer offer) => IsShipowner && offer.SelectedShipownerId == CompanyId;
    public bool CanRead(CargoOffer offer) => IsAdmin || OwnsCargo(offer)
        || (IsShipowner && (offer.Stage == OfferStage.CargoOrder
            ? offer.InvitedShipownerIds.Contains(CompanyId!.Value) : IsSelectedOwner(offer)));
    public OfferPermissions Permissions(CargoOffer offer) => new(
        (IsAdmin || OwnsCargo(offer)) && offer.Stage == OfferStage.CargoOrder,
        (OwnsCargo(offer) || IsSelectedOwner(offer)) && offer.Stage == OfferStage.Negotiation,
        OwnsCargo(offer) && offer.Stage == OfferStage.Negotiation,
        IsSelectedOwner(offer) && offer.Stage == OfferStage.Negotiation,
        OwnsCargo(offer), IsSelectedOwner(offer));
    public CargoOffer Project(CargoOffer offer)
    {
        offer.PreserveVersionForReadOnlyProjection();
        if (IsShipowner)
        {
            offer.Bids.RemoveAll(bid => bid.ShipownerId != CompanyId);
            offer.InvitedShipownerIds.RemoveAll(id => id != CompanyId);
        }
        offer.Permissions = Permissions(offer);
        return offer;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using OceanFixture.Api.Controllers;
using OceanFixture.Api.Data;
using OceanFixture.Domain;

namespace OceanFixture.Api.Security;

// [Authorize] handles authentication/roles; this filter enforces ownership on the
// bound resource, assigns server-side identity, and redacts response projections.
public sealed class OfferAccessFilter(DemoStore store) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = new OfferAccess(context.HttpContext.User);
        var action = ((ControllerActionDescriptor)context.ActionDescriptor).ActionName;
        var snapshot = store.Snapshot();
        CargoOffer? offer = null;
        if (context.ActionArguments.TryGetValue("id", out var id) && id is Guid guid)
            offer = snapshot.Offers.FirstOrDefault(item => item.Id == guid);
        else if (context.ActionArguments.TryGetValue("reference", out var reference) && reference is int number)
            offer = snapshot.Offers.FirstOrDefault(item => item.Reference == number);

        if ((id is Guid || context.ActionArguments.ContainsKey("reference")) && (offer is null || !access.CanRead(offer)))
        { context.Result = new NotFoundResult(); return; }

        if (action == nameof(OffersController.Create))
        {
            if (!access.CanCreate) { context.Result = new ForbidResult(); return; }
            var request = (CreateCargoOfferRequest)context.ActionArguments["request"]!;
            if (!access.IsAdmin && request.ChartererCompanyId is { } requested && requested != access.CompanyId)
            { context.Result = new ForbidResult(); return; }
            var companyId = access.IsAdmin ? request.ChartererCompanyId ?? 39 : access.CompanyId!.Value;
            var company = snapshot.Companies.FirstOrDefault(item => item.Id == companyId && item.Type == "Charterer" && item.Approved);
            if (company is null) { context.Result = new BadRequestObjectResult(new { message = "An approved charterer is required." }); return; }
            context.ActionArguments["request"] = request with
            {
                ChartererCompanyId = companyId,
                Terms = request.Terms is null ? null! : request.Terms with { Charterer = company.Name }
            };
        }
        if (offer is not null)
        {
            var permissions = access.Permissions(offer);
            if (action == nameof(OffersController.SubmitBid))
            {
                if (!permissions.CanSubmitBid) { context.Result = new ForbidResult(); return; }
                var request = (SubmitBidRequest)context.ActionArguments["request"]!;
                if (request.ShipownerId is { } requested && requested != access.CompanyId)
                { context.Result = new ForbidResult(); return; }
                context.ActionArguments["request"] = request with { ShipownerId = access.CompanyId };
            }
            if (action == nameof(OffersController.SelectFirmOffer) && !permissions.CanSelectFirmOffer)
            { context.Result = new ForbidResult(); return; }
            if (action == nameof(OffersController.Counter))
            { context.Result = new StatusCodeResult(StatusCodes.Status410Gone); return; }
            if (action == nameof(OffersController.SendNegotiatedCounter))
            {
                if (!permissions.CanSendCounter) { context.Result = new ForbidResult(); return; }
                var request = (NegotiatedCounterRequest)context.ActionArguments["request"]!;
                if (request.VersionHash == offer.RecapVersion && request.ChartererClauses is not null && request.ShipownerClauses is not null
                    && ((!permissions.CanEditChartererClauses && !request.ChartererClauses.SequenceEqual(offer.ChartererClauses))
                        || (!permissions.CanEditShipownerClauses && !request.ShipownerClauses.SequenceEqual(offer.ShipownerClauses))))
                { context.Result = new ForbidResult(); return; }
                context.ActionArguments["request"] = request with
                {
                    Party = access.Role,
                    SubmittedBy = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                };
            }
            if (action == nameof(OffersController.ConfirmRecap))
            {
                var request = (ConfirmRecapRequest)context.ActionArguments["request"]!;
                var party = request.Party?.Trim().ToLowerInvariant();
                if ((party != "charterer" || !permissions.CanConfirmAsCharterer)
                    && (party != "shipowner" || !permissions.CanConfirmAsShipowner))
                { context.Result = new ForbidResult(); return; }
                context.ActionArguments["request"] = request with { ConfirmedBy = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)! };
            }
        }

        var executed = await next();
        if (executed.Result is ObjectResult result)
        {
            // Recheck the response snapshot too: selection could have changed
            // between the initial access check and a read action's snapshot.
            if (result.Value is CargoOffer responseOffer && !access.CanRead(responseOffer))
            { executed.Result = new NotFoundResult(); return; }
            result.Value = result.Value switch
            {
                CargoOffer single => access.Project(single),
                IReadOnlyCollection<CargoOffer> offers => offers.Where(access.CanRead).Select(access.Project).ToArray(),
                IReadOnlyCollection<DashboardLane> lanes => lanes.Select(lane => lane with
                { Offers = lane.Offers.Where(access.CanRead).Select(access.Project).ToArray() }).ToArray(),
                _ => result.Value
            };
        }
    }
}

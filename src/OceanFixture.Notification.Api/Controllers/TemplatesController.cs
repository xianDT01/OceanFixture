using Microsoft.AspNetCore.Mvc;
using OceanFixture.Domain;

namespace OceanFixture.Notification.Api.Controllers;

[ApiController]
[Route("v1/api/message-templates")]
public sealed class TemplatesController : ControllerBase
{
    private static readonly MessageTemplate[] Templates =
    [
        new("cargo-order", "Cargo order sent to selected shipowners", "OceanFixture / Ref: ${reference} / FIRM CARGO ORDER"),
        new("firm-offer", "Firm offer sent to the selected shipowner", "OceanFixture / Ref: ${reference} / FIRM OFFER"),
        new("counter-offer", "Counter offer notification", "OceanFixture / Ref: ${reference} / COUNTER ${counter}"),
        new("recap-ready", "Recap ready for confirmation", "OceanFixture / Ref: ${reference} / RECAP READY"),
        new("fixture-confirmed", "Both parties confirmed the recap", "OceanFixture / Ref: ${reference} / FIXTURE CONFIRMATION")
    ];

    [HttpGet]
    public ActionResult<IReadOnlyCollection<MessageTemplate>> Get() => Ok(Templates);

    [HttpPost("preview")]
    public ActionResult<object> Preview(PreviewRequest request) => Ok(new
    {
        request.TemplateKey,
        Subject = Templates.FirstOrDefault(item => item.Key == request.TemplateKey)?.Subject ?? request.TemplateKey,
        request.Variables
    });
}

public sealed record PreviewRequest(string TemplateKey, Dictionary<string, string> Variables);

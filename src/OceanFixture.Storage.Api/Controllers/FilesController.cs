using Microsoft.AspNetCore.Mvc;
using OceanFixture.Domain;

namespace OceanFixture.Storage.Api.Controllers;

[ApiController]
[Route("v1/api/files")]
public sealed class FilesController : ControllerBase
{
    private static readonly List<StoredFile> Files =
    [
        new(Guid.Parse("e8f5fa57-2a22-4133-bac5-f092cf87c6a1"), "Cargo_Plan.pdf", "application/pdf", 1_423_521, DateTimeOffset.UtcNow.AddDays(-4))
    ];

    [HttpGet]
    public ActionResult<IReadOnlyCollection<StoredFile>> Get() => Ok(Files);

    [HttpGet("{id:guid}")]
    public ActionResult<StoredFile> Get(Guid id) => Files.FirstOrDefault(file => file.Id == id) is { } file ? Ok(file) : NotFound();

    [HttpPost]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<StoredFile>> Upload(IFormFile file)
    {
        await using var sink = Stream.Null;
        await file.CopyToAsync(sink);
        var stored = new StoredFile(Guid.NewGuid(), file.FileName, file.ContentType, file.Length, DateTimeOffset.UtcNow);
        Files.Add(stored);
        return CreatedAtAction(nameof(Get), new { id = stored.Id }, stored);
    }
}

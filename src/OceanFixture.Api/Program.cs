var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSingleton<OceanFixture.Api.Data.IStatePersistence>(_ =>
{
    var configuredPath = builder.Configuration["Storage:DatabasePath"] ?? "App_Data/oceanfixture.db";
    var databasePath = Path.IsPathRooted(configuredPath) ? configuredPath
        : Path.Combine(builder.Environment.ContentRootPath, configuredPath);
    return new OceanFixture.Api.Data.SqliteStatePersistence(databasePath);
});
builder.Services.AddSingleton<OceanFixture.Api.Data.DemoStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<OceanFixture.Api.Security.DemoSessionStore>();
builder.Services.AddScoped<OceanFixture.Api.Security.OfferAccessFilter>();
builder.Services.AddAuthentication("DemoBearer").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
    OceanFixture.Api.Security.DemoBearerHandler>("DemoBearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
// Load eagerly: damaged or inaccessible storage must fail startup, not reseed silently.
app.Services.GetRequiredService<OceanFixture.Api.Data.DemoStore>();

// Configure the HTTP request pipeline.

app.UseCors();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var writeFailed = error is OceanFixture.Api.Data.PersistenceWriteException;
    await Results.Problem(statusCode: writeFailed ? 503 : 500,
        title: writeFailed ? "The change could not be saved. Please try again." : "An unexpected error occurred.")
        .ExecuteAsync(context);
}));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "OceanFixture.Api", status = "healthy" }));

app.Run();

using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using PalworldHelper.App;
using PalworldHelper.Core.Abstractions;
using PalworldHelper.Data;
using PalworldHelper.Data.Persistence;
using PalworldHelper.SaveImport;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://127.0.0.1:0");
var pathProvider = new AppDataPathProvider();
builder.Services.AddSingleton(pathProvider);
builder.Services.AddPalworldHelperData(pathProvider.DatabasePath);
builder.Services.AddPalworldHelperSaveImport();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/about", () =>
{
    var assembly = Assembly.GetExecutingAssembly().GetName();
    return Results.Ok(new
    {
        name = assembly.Name,
        version = assembly.Version?.ToString() ?? "development",
        runtime = Environment.Version.ToString(),
        platform = Environment.OSVersion.ToString()
    });
});

app.MapGet("/api/stats", async (PalworldHelperDbContext db, CancellationToken cancellationToken) =>
{
    var species = await db.PalSpecies.CountAsync(cancellationToken);
    var recipes = await db.BreedingRecipes.CountAsync(cancellationToken);
    var collections = await db.PlayerCollections.CountAsync(cancellationToken);
    var pals = await db.PalInstances.CountAsync(cancellationToken);

    return Results.Ok(new { species, recipes, collections, pals });
});

app.MapFallbackToFile("index.html");

await app.Services.InitializePalworldHelperDataAsync();
await app.StartAsync();

var server = app.Services.GetRequiredService<IServer>();
var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
    ?? "http://127.0.0.1:5000";
Console.WriteLine($"PalworldHelper is running at {address}");

TryOpenBrowser(address);
await app.WaitForShutdownAsync();

static void TryOpenBrowser(string address)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = address,
            UseShellExecute = true
        });
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Could not open the browser automatically: {exception.Message}");
    }
}

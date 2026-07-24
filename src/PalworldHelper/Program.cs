using System.Diagnostics;
using System.Net;
using PalworldHelper.Contracts;
using PalworldHelper.Data;
using PalworldHelper.Models;
using PalworldHelper.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:8765");
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<BreedingDataService>();
builder.Services.AddSingleton<BreedingEngine>();
builder.Services.AddSingleton<SshSaveDownloader>();
builder.Services.AddSingleton<SaveImportService>();

var app = builder.Build();
await app.Services.GetRequiredService<Database>().InitializeAsync();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", async (Database db) =>
{
    var profiles = await db.GetServerProfilesAsync();
    return Results.Ok(new { version = "0.1.0", profiles = profiles.Count, ready = true });
});

app.MapGet("/api/servers", async (Database db) => Results.Ok(await db.GetServerProfilesAsync()));
app.MapPost("/api/servers", async (SaveServerRequest request, Database db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.Username))
        return Results.BadRequest(new { error = "Name, host and SSH username are required." });
    var id = await db.UpsertServerProfileAsync(new ServerProfile(request.Id ?? 0, request.Name.Trim(), request.Host.Trim(), request.Port <= 0 ? 22 : request.Port,
        request.Username.Trim(), request.RemoteSavePath.Trim(), string.IsNullOrWhiteSpace(request.PlayerName) ? "Lanessear" : request.PlayerName.Trim(),
        string.IsNullOrWhiteSpace(request.PrivateKeyPath) ? null : request.PrivateKeyPath.Trim(), null));
    return Results.Ok(new { id });
});

app.MapPost("/api/sync", async (SyncRequest request, Database db, SshSaveDownloader ssh, SaveImportService importer, CancellationToken ct) =>
{
    var profile = await db.GetServerProfileAsync(request.ServerProfileId);
    if (profile is null) return Results.NotFound(new { error = "Server profile not found." });
    string? temp = null;
    try
    {
        temp = await ssh.DownloadAsync(profile, request.Password, request.PrivateKeyPassphrase, ct);
        var pals = await importer.ImportAsync(temp, profile.Id, profile.PlayerName, ct);
        await db.ReplaceOwnedPalsAsync(profile.Id, pals);
        return Results.Ok(new { imported = pals.Count, player = profile.PlayerName, syncedAt = DateTimeOffset.UtcNow });
    }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 500); }
    finally
    {
        if (!string.IsNullOrEmpty(temp))
        {
            try { Directory.Delete(Path.GetDirectoryName(temp)!, true); } catch { }
        }
    }
});

app.MapPost("/api/import-json/{profileId:long}", async (long profileId, HttpRequest request, Database db, SaveImportService importer, CancellationToken ct) =>
{
    var profile = await db.GetServerProfileAsync(profileId);
    if (profile is null) return Results.NotFound(new { error = "Server profile not found." });
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Upload a converted Level.sav JSON file." });
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest(new { error = "No file uploaded." });
    var temp = Path.Combine(Path.GetTempPath(), "PalworldHelper", Guid.NewGuid().ToString("N") + ".json");
    Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
    await using (var output = File.Create(temp)) await file.CopyToAsync(output, ct);
    try
    {
        var pals = await importer.ImportConvertedJsonAsync(temp, profile.Id, profile.PlayerName, ct);
        await db.ReplaceOwnedPalsAsync(profile.Id, pals);
        return Results.Ok(new { imported = pals.Count });
    }
    finally { File.Delete(temp); }
});

app.MapGet("/api/collection/{profileId:long}", async (long profileId, Database db) => Results.Ok(await db.GetOwnedPalsAsync(profileId)));
app.MapGet("/api/catalog", async (BreedingDataService service) => Results.Ok(new { pals = await service.GetPalNamesAsync() }));
app.MapGet("/api/passives/{profileId:long}", async (long profileId, Database db) =>
{
    var pals = await db.GetOwnedPalsAsync(profileId);
    return Results.Ok(pals.SelectMany(x => x.PassiveSkills).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x));
});
app.MapPost("/api/breeding/search", async (BreedingSearchRequest request, Database db, BreedingEngine engine) =>
{
    var owned = await db.GetOwnedPalsAsync(request.ServerProfileId);
    var routes = await engine.FindRoutesAsync(request.TargetPal, request.PassiveSkills ?? [], owned, request.UseOnlyOwnedPals,
        Math.Clamp(request.MaxDepth, 1, 10), Math.Clamp(request.MaxResults, 1, 50));
    return Results.Ok(routes);
});

app.MapFallbackToFile("index.html");

if (!args.Contains("--no-browser", StringComparer.OrdinalIgnoreCase))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { Process.Start(new ProcessStartInfo("http://127.0.0.1:8765") { UseShellExecute = true }); } catch { }
    });
}
await app.RunAsync();

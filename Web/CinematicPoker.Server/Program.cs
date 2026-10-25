using CinematicPoker.Server;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        .SetIsOriginAllowed(_ => true)));

var app = builder.Build();
app.UseCors();
app.MapGet("/health", () => Results.Ok(new
{
    service = "Dead Man's Hand multiplayer",
    status = "ready",
    transport = "SignalR",
    note = "In-memory beta rooms; restart clears rooms."
}));
// Solo play runs in the browser; the client posts here so Render logs
// can see single-player sessions the same way the hub logs multiplayer.
app.MapPost("/usage", (UsageReport body, ILoggerFactory logs) =>
{
    var log = logs.CreateLogger("GameUsage");
    string raw = string.Join(" ", (body?.Player ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    string player = new string(raw.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').Take(16).ToArray());
    if (player.Length == 0) player = "Player";
    string mode = (body?.Mode ?? "solo").Trim().ToLowerInvariant();
    mode = mode switch
    {
        "solo_resume" or "resume" => "solo_resume",
        "host_table" or "host" => "host_table",
        _ => "solo"
    };
    // Keep the greppable "Game solo" prefix for the common case; resume/host
    // variants stay under the same Game … family for Render search.
    log.LogInformation(
        "Game solo started at={At:o} player={Player} mode={Mode}",
        DateTimeOffset.UtcNow, player, mode);
    return Results.NoContent();
});
app.MapHub<PokerHub>("/gamehub");

// Local beta convenience: serve the built WASM app beside the hub so an
// iPhone only needs one public HTTPS URL. Production can put both behind a
// proper reverse proxy instead.
var configuredWebRoot = Environment.GetEnvironmentVariable("DMH_WEB_ROOT");
var webRoot = !string.IsNullOrWhiteSpace(configuredWebRoot)
    ? Path.GetFullPath(configuredWebRoot)
    : Path.GetFullPath("../CinematicPoker.Web/wwwroot", app.Environment.ContentRootPath);
if (Directory.Exists(webRoot))
{
    var provider = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    // .glb models, .dll/.dat/.blat Blazor payloads etc. have no built-in MIME
    // type; without this Kestrel answers 404 and the 3D scene never loads.
    var contentTypes = new FileExtensionContentTypeProvider();
    contentTypes.Mappings[".glb"] = "model/gltf-binary";
    contentTypes.Mappings[".gltf"] = "model/gltf+json";
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = provider,
        ContentTypeProvider = contentTypes,
        ServeUnknownFileTypes = true,
        DefaultContentType = "application/octet-stream"
    });
    app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"));
    });
}
app.Run();

internal sealed record UsageReport(string Player, string Mode);

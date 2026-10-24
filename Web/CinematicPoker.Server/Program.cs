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

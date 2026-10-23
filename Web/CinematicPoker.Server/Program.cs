using CinematicPoker.Server;
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
var webRoot = Path.GetFullPath(
    "../CinematicPoker.Web/wwwroot",
    app.Environment.ContentRootPath);
if (Directory.Exists(webRoot))
{
    var provider = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
    app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(webRoot, "index.html"));
    });
}
app.Run();

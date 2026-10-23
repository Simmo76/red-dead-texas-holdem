using CinematicPoker.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        .SetIsOriginAllowed(_ => true)));

var app = builder.Build();
app.UseCors();
app.MapGet("/", () => Results.Ok(new
{
    service = "Dead Man's Hand multiplayer",
    status = "ready",
    transport = "SignalR",
    note = "In-memory beta rooms; restart clears rooms."
}));
app.MapHub<PokerHub>("/gamehub");
app.Run();

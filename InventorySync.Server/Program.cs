using InventorySync.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// CORS — allow plugin clients to connect from any origin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

// Bind to PORT env var if set (Railway injects this)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseCors();
app.MapHub<InventoryHub>("/inventory");

// Health check endpoint — Railway uses this to confirm the service is up
app.MapGet("/", () => "InventorySync relay OK");

app.Run();

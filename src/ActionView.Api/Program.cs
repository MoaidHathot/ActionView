using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Api;
using ActionView.Api.Endpoints;
using ActionView.Api.Hubs;
using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Load ActionView configuration
var configPath = args.FirstOrDefault(a => a.StartsWith("--config="))?.Split('=', 2)[1];
var appsettingsConfigPath = builder.Configuration.GetValue<string>("ActionView:ConfigPath");
var config = ConfigLoader.Load(configPath, appsettingsConfigPath);

// JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// Register services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<SecretResolver>();
builder.Services.AddSingleton(sp =>
    new TemplateRegistry(config.DataDirectory, sp.GetRequiredService<ILogger<TemplateRegistry>>()));
builder.Services.AddSingleton<EntryNormalizer>();
builder.Services.AddSingleton(sp =>
    new EntryStore(config.DataDirectory, sp.GetRequiredService<ILogger<EntryStore>>(),
        sp.GetRequiredService<EntryNormalizer>()));
builder.Services.AddSingleton(sp =>
    new InboxWatcher(config.DataDirectory, sp.GetRequiredService<EntryStore>(), sp.GetRequiredService<ILogger<InboxWatcher>>()));
builder.Services.AddSingleton<ActionExecutor>();
builder.Services.AddHttpClient<ActionExecutor>();

// SignalR
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// WebSocket support (needed for Vite HMR proxy and SignalR)
app.UseWebSockets();

// In development, launch Vite and proxy non-API requests to it.
// In production, serve the built React files embedded in the assembly.
ManifestEmbeddedFileProvider? embeddedProvider = null;
if (app.Environment.IsDevelopment())
{
    // Resolve client directory relative to the project root
    var clientDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "client"));
    app.UseViteDev(clientDir);
}
else
{
    embeddedProvider = new ManifestEmbeddedFileProvider(
        typeof(Program).Assembly, "wwwroot");
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedProvider });
}

// Map endpoints
app.MapEntryEndpoints();
app.MapHistoryEndpoints();
app.MapStatsEndpoints();
app.MapHub<EntryHub>("/hubs/entries");

// SPA fallback: serve index.html for non-API, non-file routes so
// client-side routing works on refresh / deep links.
if (embeddedProvider is not null)
{
    app.MapFallback(async context =>
    {
        var fileInfo = embeddedProvider.GetFileInfo("index.html");
        if (fileInfo.Exists)
        {
            context.Response.ContentType = "text/html";
            await using var stream = fileInfo.CreateReadStream();
            await stream.CopyToAsync(context.Response.Body);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });
}

// Start inbox watcher and wire up SignalR notifications
var inboxWatcher = app.Services.GetRequiredService<InboxWatcher>();
var entryStore = app.Services.GetRequiredService<EntryStore>();
var templateRegistry = app.Services.GetRequiredService<TemplateRegistry>();
var hubContext = app.Services.GetRequiredService<IHubContext<EntryHub, IEntryHubClient>>();

inboxWatcher.EntriesReceived += entries =>
{
    _ = hubContext.Clients.All.EntriesAdded(entries);
};

// Watch active directory for external changes (e.g. CLI deletes)
entryStore.EntriesExternallyDeleted += entryIds =>
{
    foreach (var id in entryIds)
        _ = hubContext.Clients.All.EntryDeleted(id);
};

entryStore.EntriesExternallyAdded += entries =>
{
    _ = hubContext.Clients.All.EntriesAdded(entries);
};

entryStore.EntryExternallyUpdated += entry =>
{
    _ = hubContext.Clients.All.EntryUpdated(entry);
};

templateRegistry.StartWatching();
entryStore.StartWatchingActive();
inboxWatcher.Start();

app.Lifetime.ApplicationStopping.Register(() =>
{
    inboxWatcher.Stop();
    inboxWatcher.Dispose();
    templateRegistry.Dispose();
    entryStore.Dispose();
});

app.Run("http://localhost:5173");

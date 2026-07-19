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

// Resolve the listen URL.
// Precedence (highest first):
//   1. --urls <url>          (standard ASP.NET Core flag, full URL)
//   2. --port <port>         (shortcut, binds to http://localhost:<port>)
//   3. config.ListenUrl      (from actionview.json)
static string? ReadArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == name && i + 1 < args.Length)
            return args[i + 1];
        if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
            return args[i].Substring(name.Length + 1);
    }
    return null;
}

var urlsArg = ReadArgValue(args, "--urls");
var portArg = ReadArgValue(args, "--port");

string listenUrl;
if (!string.IsNullOrWhiteSpace(urlsArg))
    listenUrl = urlsArg;
else if (!string.IsNullOrWhiteSpace(portArg) && int.TryParse(portArg, out var port))
    listenUrl = $"http://localhost:{port}";
else
    listenUrl = config.ListenUrl;

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
builder.Services.AddSingleton<ParameterResolver>();
builder.Services.AddSingleton<FileAccessResolver>();
builder.Services.AddSingleton(sp =>
    new TemplateRegistry(config.DataDirectory, sp.GetRequiredService<ILogger<TemplateRegistry>>()));
builder.Services.AddSingleton<EntryNormalizer>();
builder.Services.AddSingleton<EntryValidator>();
builder.Services.AddSingleton(sp =>
    new EntryStore(config.DataDirectory, sp.GetRequiredService<ILogger<EntryStore>>(),
        sp.GetRequiredService<EntryNormalizer>(),
        sp.GetRequiredService<EntryValidator>(),
        config.Ingest.Strict));
builder.Services.AddSingleton(sp =>
    new InboxWatcher(config.DataDirectory, sp.GetRequiredService<EntryStore>(), sp.GetRequiredService<ILogger<InboxWatcher>>()));
builder.Services.AddSingleton<ActionExecutor>();
builder.Services.AddHttpClient<ActionExecutor>();
builder.Services.AddSingleton<ToastNotifier>();
builder.Services.AddSingleton<ViewStore>();
builder.Services.AddSingleton<ConfigWatcher>();

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

// Only launch Vite when running from the source tree. Packaged tool runs can
// inherit a Development environment from the shell, but they only have the
// embedded client assets available.
var clientDir = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "client"));
var useViteDev = app.Environment.IsDevelopment() &&
    File.Exists(Path.Combine(clientDir, "package.json"));

if (useViteDev)
{
    app.UseViteDev(clientDir);
}
else
{
    if (app.Environment.IsDevelopment())
        app.Logger.LogInformation("Client source directory not found; serving embedded dashboard assets instead.");

    embeddedProvider = new ManifestEmbeddedFileProvider(
        typeof(Program).Assembly, "wwwroot");
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = embeddedProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = embeddedProvider });
}

// Map endpoints
app.MapEntryEndpoints();
app.MapHistoryEndpoints();
app.MapStatsEndpoints();
app.MapFileEndpoints();
app.MapExportEndpoints();
app.MapViewEndpoints();
app.MapConfigEndpoints();
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
var toastNotifier = app.Services.GetRequiredService<ToastNotifier>();

inboxWatcher.EntriesReceived += entries =>
{
    _ = hubContext.Clients.All.EntriesAdded(entries);
    toastNotifier.NotifyEntries(entries);
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

templateRegistry.StartWatching();

// Scan external templates directory if configured (after StartWatching so the
// watcher sees the final state via its own Created/Changed events).
if (config.Templates.ExternalDirectory is not null)
{
    var scanner = new TemplateScanner(
        templateRegistry, config.DataDirectory,
        app.Services.GetRequiredService<ILogger<TemplateScanner>>());
    scanner.Scan(config.Templates.ExternalDirectory, config.Templates.Recursive);
}

entryStore.StartWatchingActive();
inboxWatcher.Start();

// Watch actionview.json for external edits and hot-reload the runtime-safe
// slices (views / tag-match / notifications / secrets). Push to dashboards so
// they re-fetch. Gated by config.WatchConfig (default true).
ConfigWatcher? configWatcher = null;
if (config.WatchConfig)
{
    configWatcher = app.Services.GetRequiredService<ConfigWatcher>();
    configWatcher.ConfigChanged += () => _ = hubContext.Clients.All.ConfigChanged();
    configWatcher.StartWatching();
}

app.Lifetime.ApplicationStopping.Register(() =>
{
    inboxWatcher.Stop();
    inboxWatcher.Dispose();
    templateRegistry.Dispose();
    entryStore.Dispose();
    configWatcher?.Dispose();
});

app.Run(listenUrl);

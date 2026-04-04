using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ActionView.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Parse --read-only and --config before Host takes over
var readOnly = args.Any(a => a.Equals("--read-only", StringComparison.OrdinalIgnoreCase));
var configPath = ExtractOption(args, "--config");

var config = ConfigLoader.Load(configPath);
ConfigLoader.EnsureDirectories(config.DataDirectory);

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "0.0.0";

var builder = Host.CreateApplicationBuilder(args);

// Logging must go to stderr for stdio transport
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// JSON serializer options matching ActionView conventions
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};

// Register Core services
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(jsonOptions);
builder.Services.AddSingleton<TemplateRegistry>(sp =>
    new TemplateRegistry(config.DataDirectory, sp.GetRequiredService<ILogger<TemplateRegistry>>()));
builder.Services.AddSingleton<EntryNormalizer>(sp =>
    new EntryNormalizer(
        sp.GetRequiredService<TemplateRegistry>(),
        sp.GetRequiredService<ILogger<EntryNormalizer>>()));
builder.Services.AddSingleton<EntryStore>(sp =>
    new EntryStore(
        config.DataDirectory,
        sp.GetRequiredService<ILogger<EntryStore>>(),
        sp.GetRequiredService<EntryNormalizer>()));

// Register MCP server with stdio transport
var mcpBuilder = builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "actionview",
            Version = version
        };
    })
    .WithStdioServerTransport()
    .WithTools<EntryReadTools>()
    .WithTools<TemplateReadTools>()
    .WithTools<StatsTools>();

if (!readOnly)
{
    mcpBuilder
        .WithTools<EntryWriteTools>()
        .WithTools<TemplateWriteTools>();
}

await builder.Build().RunAsync();

// --- Helpers ---

static string? ExtractOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

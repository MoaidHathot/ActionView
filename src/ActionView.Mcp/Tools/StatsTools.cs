using System.ComponentModel;
using System.Text.Json;
using ActionView.Core.Services;
using ModelContextProtocol.Server;

namespace ActionView.Mcp.Tools;

[McpServerToolType]
public sealed class StatsTools
{
    [McpServerTool(Name = "get_stats", ReadOnly = true), Description(
        "Get dashboard statistics for the ActionView review queue. " +
        "Returns counts of pending, viewed, and archived entries, grouped by type and severity.")]
    public static string GetStats(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions)
    {
        var stats = entryStore.GetStats();
        return JsonSerializer.Serialize(stats, jsonOptions);
    }

    [McpServerTool(Name = "get_schema", ReadOnly = true), Description(
        "Get the JSON schema for ActionView entries. " +
        "Use this to understand the full entry structure when constructing entries for add_entry.")]
    public static string GetSchema()
    {
        using var stream = typeof(StatsTools).Assembly.GetManifestResourceStream("entry.v1.schema.json");
        if (stream is null)
            return """{"error": "Embedded schema resource not found"}""";

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

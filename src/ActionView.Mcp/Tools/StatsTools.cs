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
        "Use this to understand the full entry structure when constructing entries for add_entry. " +
        "Tip: for large or complex entries, prefer validate_entry (submit best-effort JSON and fix the " +
        "reported errors) rather than reasoning about this entire schema up front.")]
    public static string GetSchema() => EntrySchemaProvider.RawJson;
}

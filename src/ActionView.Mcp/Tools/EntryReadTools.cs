using System.ComponentModel;
using System.Text.Json;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ModelContextProtocol.Server;

namespace ActionView.Mcp.Tools;

[McpServerToolType]
public sealed class EntryReadTools
{
    [McpServerTool(Name = "list_entries", ReadOnly = true), Description(
        "List active entries in the ActionView review queue. " +
        "Returns entries sorted by pinned status, priority, severity, and creation date " +
        "unless an explicit sort is given. All parameters are optional.")]
    public static string ListEntries(
        EntryStore entryStore,
        AppConfig config,
        JsonSerializerOptions jsonOptions,
        [Description("Filter by entry type (e.g., 'pr-review', 'deploy', 'incident')")] string? type = null,
        [Description("Filter by severity: low, medium, high, critical")] string? severity = null,
        [Description("Filter by source system name")] string? source = null,
        [Description("Filter by tags (comma-separated)")] string? tags = null,
        [Description("Tag match mode: 'any' (OR) or 'all' (AND). Defaults to the server config.")] string? tagMode = null,
        [Description("Search in title, subtitle, source, type, and tags")] string? search = null,
        [Description("Apply a saved view by id or name (supplies type + tags)")] string? view = null,
        [Description("Sort field: created, priority, severity, title")] string? sort = null,
        [Description("Sort direction: asc or desc")] string? dir = null)
    {
        var criteria = EntryFiltering.ResolveCriteria(
            config.Views, config.TagMatchMode, view, type, severity, source, tags, tagMode, search);

        var entries = EntryQuery.RunActive(
            entryStore.GetActiveEntries(), criteria,
            EntrySorting.TryParseField(sort), EntrySorting.ParseDirection(dir));

        return JsonSerializer.Serialize(new { count = entries.Count, entries }, jsonOptions);
    }

    [McpServerTool(Name = "get_entry", ReadOnly = true), Description(
        "Get a single active entry by its ID. " +
        "Returns the full entry JSON including content blocks and actions. " +
        "Use list_entries first to find entry IDs.")]
    public static string GetEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry ID (full or partial prefix match)")] string id)
    {
        var entry = entryStore.GetEntry(id);
        if (entry is not null)
            return JsonSerializer.Serialize(entry, jsonOptions);

        // Try partial match
        var allEntries = entryStore.GetActiveEntries();
        var candidates = allEntries
            .Where(e => e.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 1)
            return JsonSerializer.Serialize(candidates[0], jsonOptions);

        if (candidates.Count > 1)
        {
            var ids = candidates.Select(e => e.Id).ToList();
            return JsonSerializer.Serialize(new { error = "Ambiguous ID", matches = ids }, jsonOptions);
        }

        return JsonSerializer.Serialize(new { error = $"Entry not found: {id}" }, jsonOptions);
    }
}

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
        "Returns entries sorted by pinned status, priority, severity, and creation date. " +
        "All filter parameters are optional.")]
    public static string ListEntries(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("Filter by entry type (e.g., 'pr-review', 'deploy', 'incident')")] string? type = null,
        [Description("Filter by severity: low, medium, high, critical")] string? severity = null,
        [Description("Filter by source system name")] string? source = null,
        [Description("Search in title, subtitle, source, and tags")] string? search = null)
    {
        var entries = entryStore.GetActiveEntries().ToList();

        if (!string.IsNullOrWhiteSpace(type))
            entries = entries.Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
            entries = entries.Where(e => e.Severity == sev).ToList();

        if (!string.IsNullOrWhiteSpace(source))
            entries = entries.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            entries = entries.Where(e =>
                e.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.Subtitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.Source.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

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

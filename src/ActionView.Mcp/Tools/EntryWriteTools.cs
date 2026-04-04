using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using ModelContextProtocol.Server;

namespace ActionView.Mcp.Tools;

[McpServerToolType]
public sealed class EntryWriteTools
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [McpServerTool(Name = "add_entry"), Description(
        "Add a new entry to the ActionView review queue. " +
        "The entry is ingested, normalized against its type template, and made active. " +
        "Required fields: type, source, title. " +
        "Use get_schema to see the full entry JSON schema.")]
    public static string AddEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry JSON string containing at minimum: type, source, and title fields")] string entryJson)
    {
        Entry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<Entry>(entryJson, ReadOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid JSON: {ex.Message}" }, jsonOptions);
        }

        if (entry is null)
            return JsonSerializer.Serialize(new { error = "Failed to deserialize entry" }, jsonOptions);

        if (string.IsNullOrWhiteSpace(entry.Type) ||
            string.IsNullOrWhiteSpace(entry.Source) ||
            string.IsNullOrWhiteSpace(entry.Title))
        {
            return JsonSerializer.Serialize(new { error = "Missing required fields: type, source, and title" }, jsonOptions);
        }

        var result = entryStore.IngestEntry(entry);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Failed to ingest entry" }, jsonOptions);

        return JsonSerializer.Serialize(new
        {
            success = true,
            id = result.Id,
            title = result.Title,
            type = result.Type,
            severity = result.Severity.ToString().ToLowerInvariant()
        }, jsonOptions);
    }

    [McpServerTool(Name = "dismiss_entry"), Description(
        "Dismiss an active entry, moving it to the archive. " +
        "Use list_entries to find entry IDs.")]
    public static string DismissEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry ID to dismiss")] string id,
        [Description("Optional reason for dismissal")] string? reason = null)
    {
        var outcome = new EntryOutcome
        {
            Action = "Dismissed",
            Success = true,
            ResultMessage = reason ?? "Dismissed via MCP"
        };

        var entry = entryStore.ArchiveEntry(id, outcome);
        if (entry is null)
            return JsonSerializer.Serialize(new { error = $"Entry not found: {id}" }, jsonOptions);

        return JsonSerializer.Serialize(new
        {
            success = true,
            id = entry.Id,
            title = entry.Title,
            status = "archived"
        }, jsonOptions);
    }

    [McpServerTool(Name = "delete_entry", Destructive = true), Description(
        "Permanently delete an active entry. This cannot be undone. " +
        "Use dismiss_entry instead if you want to archive the entry.")]
    public static string DeleteEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry ID to permanently delete")] string id)
    {
        var deleted = entryStore.DeleteEntry(id);
        if (!deleted)
            return JsonSerializer.Serialize(new { error = $"Entry not found: {id}" }, jsonOptions);

        return JsonSerializer.Serialize(new { success = true, id, status = "deleted" }, jsonOptions);
    }

    [McpServerTool(Name = "pin_entry", Idempotent = true), Description(
        "Toggle the pinned state of an active entry. " +
        "Pinned entries appear at the top of the list.")]
    public static string PinEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry ID to pin or unpin")] string id)
    {
        var entry = entryStore.TogglePin(id);
        if (entry is null)
            return JsonSerializer.Serialize(new { error = $"Entry not found: {id}" }, jsonOptions);

        return JsonSerializer.Serialize(new
        {
            success = true,
            id = entry.Id,
            title = entry.Title,
            pinned = entry.Pinned
        }, jsonOptions);
    }
}

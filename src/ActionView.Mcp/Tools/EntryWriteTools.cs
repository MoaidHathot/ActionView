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

    [McpServerTool(Name = "update_entry", Idempotent = true), Description(
        "Update fields of an existing active entry in place. " +
        "Supply a JSON object string with only the fields you want to change; " +
        "omitted (or null) fields are left untouched. " +
        "Updatable fields: title, subtitle, severity, tags, content, actions, priority. " +
        "Identity fields (id, type, source, createdAt) cannot be changed. " +
        "Use add_entry to create new entries; use update_entry to modify existing ones.")]
    public static string UpdateEntry(
        EntryStore entryStore,
        JsonSerializerOptions jsonOptions,
        [Description("The entry ID")] string id,
        [Description("JSON object string with fields to update, e.g. '{\"severity\":\"high\",\"tags\":[\"urgent\"]}'")] string updateJson)
    {
        EntryUpdateRequest? update;
        try
        {
            update = JsonSerializer.Deserialize<EntryUpdateRequest>(updateJson, ReadOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid update JSON: {ex.Message}" }, jsonOptions);
        }

        if (update is null)
            return JsonSerializer.Serialize(new { error = "Failed to deserialize update payload" }, jsonOptions);

        // Track which fields the caller actually supplied so the response can confirm what changed.
        var fieldsUpdated = new List<string>();

        var entry = entryStore.UpdateEntry(id, e =>
        {
            if (update.Title is not null) { e.Title = update.Title; fieldsUpdated.Add("title"); }
            if (update.Subtitle is not null) { e.Subtitle = update.Subtitle; fieldsUpdated.Add("subtitle"); }
            if (update.Severity.HasValue) { e.Severity = update.Severity.Value; fieldsUpdated.Add("severity"); }
            if (update.Tags is not null) { e.Tags = update.Tags; fieldsUpdated.Add("tags"); }
            if (update.Content is not null) { e.Content = update.Content; fieldsUpdated.Add("content"); }
            if (update.Actions is not null) { e.Actions = update.Actions; fieldsUpdated.Add("actions"); }
            if (update.Priority.HasValue) { e.Priority = update.Priority.Value; fieldsUpdated.Add("priority"); }
        });

        if (entry is null)
            return JsonSerializer.Serialize(new { error = $"Entry not found or not active: {id}" }, jsonOptions);

        return JsonSerializer.Serialize(new
        {
            success = true,
            id = entry.Id,
            title = entry.Title,
            type = entry.Type,
            severity = entry.Severity.ToString().ToLowerInvariant(),
            fieldsUpdated
        }, jsonOptions);
    }
}

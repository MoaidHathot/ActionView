using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.AspNetCore.SignalR;
using ActionView.Api.Hubs;

namespace ActionView.Api.Endpoints;

public static class EntryEndpoints
{
    public static void MapEntryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/entries");

        // --- List active entries with filtering + optional sort ---
        group.MapGet("/", (EntryStore store, AppConfig config,
            string? type, string? severity, string? source, string? tags, string? search,
            string? tagMode, string? sort, string? dir) =>
        {
            var criteria = EntryFiltering.ParseCriteria(
                type, severity, source, tags, tagMode, search, config.TagMatchMode);

            var entries = EntryQuery.RunActive(
                store.GetActiveEntries(), criteria,
                EntrySorting.TryParseField(sort), EntrySorting.ParseDirection(dir));

            return Results.Ok(entries);
        });

        // --- Get single entry (marks as viewed) ---
        group.MapGet("/{id}", (string id, EntryStore store) =>
        {
            var entry = store.GetEntry(id);
            if (entry is null) return Results.NotFound();

            store.MarkViewed(id);
            return Results.Ok(entry);
        });

        // --- Create entry via webhook (POST /api/entries) ---
        group.MapPost("/", async (Entry entry, EntryStore store,
            IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var ingested = store.IngestEntry(entry);
            if (ingested is null)
                return Results.BadRequest(new { error = "Invalid entry. Required fields: type, source, title." });

            await hubContext.Clients.All.EntriesAdded(new List<Entry> { ingested });
            return Results.Created($"/api/entries/{ingested.Id}", ingested);
        });

        // --- Batch create entries via webhook (POST /api/entries/batch) ---
        group.MapPost("/batch/ingest", async (List<Entry> entries, EntryStore store,
            IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var ingested = new List<Entry>();
            foreach (var entry in entries)
            {
                var result = store.IngestEntry(entry);
                if (result is not null)
                    ingested.Add(result);
            }

            if (ingested.Count > 0)
                await hubContext.Clients.All.EntriesAdded(ingested);

            return Results.Ok(new { ingested = ingested.Count, failed = entries.Count - ingested.Count });
        });

        // --- Update entry (PUT /api/entries/{id}) ---
        group.MapPut("/{id}", async (string id, EntryUpdateRequest update, EntryStore store,
            IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.UpdateEntry(id, e =>
            {
                if (update.Title is not null) e.Title = update.Title;
                if (update.Subtitle is not null) e.Subtitle = update.Subtitle;
                if (update.Severity.HasValue) e.Severity = update.Severity.Value;
                if (update.Tags is not null) e.Tags = update.Tags;
                if (update.Content is not null) e.Content = update.Content;
                if (update.Actions is not null) e.Actions = update.Actions;
                if (update.Priority.HasValue) e.Priority = update.Priority.Value;
            });

            if (entry is null) return Results.NotFound();

            await hubContext.Clients.All.EntryUpdated(entry);
            return Results.Ok(entry);
        });

        // --- Execute entry action ---
        group.MapPost("/{id}/actions/{actionIndex:int}",
            async (string id, int actionIndex, ActionExecutionRequest? request,
                   EntryStore store, ActionExecutor executor,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.GetEntry(id);
            if (entry is null) return Results.NotFound(new { error = "Entry not found" });

            if (actionIndex < 0 || actionIndex >= entry.Actions.Count)
                return Results.BadRequest(new { error = "Invalid action index" });

            var action = entry.Actions[actionIndex];

            var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request?.Parameters);
            if (errors.Count > 0)
                return Results.BadRequest(new { error = "Invalid parameters", details = errors });

            var result = await executor.ExecuteAsync(action.Command, parameters);

            if (result.Success)
            {
                switch (action.OnSuccess)
                {
                    case PostActionBehavior.Archive:
                        var archivedEntry = store.ArchiveEntry(id, new EntryOutcome
                        {
                            Action = action.Label,
                            Success = true,
                            ResultMessage = result.Message
                        });
                        if (archivedEntry is not null)
                            await hubContext.Clients.All.EntryArchived(archivedEntry);
                        break;

                    case PostActionBehavior.Delete:
                        store.DeleteEntry(id);
                        await hubContext.Clients.All.EntryDeleted(id);
                        break;

                    case PostActionBehavior.Keep:
                        break;
                }
            }

            return Results.Ok(result);
        });

        // --- Execute section action ---
        group.MapPost("/{entryId}/sections/{sectionIndex:int}/actions/{actionIndex:int}",
            async (string entryId, int sectionIndex, int actionIndex, ActionExecutionRequest? request,
                   EntryStore store, ActionExecutor executor) =>
        {
            var entry = store.GetEntry(entryId);
            if (entry is null) return Results.NotFound(new { error = "Entry not found" });

            var sections = entry.Content.Where(c => c.Type == ContentBlockType.Section).ToList();
            if (sectionIndex < 0 || sectionIndex >= sections.Count)
                return Results.BadRequest(new { error = "Invalid section index" });

            var section = sections[sectionIndex];
            if (section.Actions is null || actionIndex < 0 || actionIndex >= section.Actions.Count)
                return Results.BadRequest(new { error = "Invalid action index" });

            var action = section.Actions[actionIndex];

            var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request?.Parameters);
            if (errors.Count > 0)
                return Results.BadRequest(new { error = "Invalid parameters", details = errors });

            var result = await executor.ExecuteAsync(action.Command, parameters);

            return Results.Ok(result);
        });

        // --- Pin/unpin entry ---
        group.MapPost("/{id}/pin",
            async (string id, EntryStore store, IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.TogglePin(id);
            if (entry is null) return Results.NotFound();

            await hubContext.Clients.All.EntryUpdated(entry);
            return Results.Ok(entry);
        });

        // --- Undo action (unarchive) ---
        group.MapPost("/{id}/undo",
            async (string id, ActionExecutionRequest? request,
                   EntryStore store, ActionExecutor executor,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            // Try to find the archived entry to get the undo command info
            var archivedEntry = store.GetArchivedEntry(id);
            if (archivedEntry is null) return Results.NotFound(new { error = "Archived entry not found" });

            // Find the action that was taken and check if it has an undo command
            ActionCommand? undoCommand = null;
            EntryAction? action = null;
            if (archivedEntry.Outcome is not null)
            {
                action = archivedEntry.Actions.FirstOrDefault(a =>
                    a.Label == archivedEntry.Outcome.Action && a.UndoCommand is not null);
                undoCommand = action?.UndoCommand;
            }

            // Execute undo command if present
            if (undoCommand is not null)
            {
                var (errors, parameters) = ActionParameterValidator.Validate(action?.Parameters, request?.Parameters);
                if (errors.Count > 0)
                    return Results.BadRequest(new { error = "Invalid parameters", details = errors });

                var undoResult = await executor.ExecuteAsync(undoCommand, parameters);
                if (!undoResult.Success)
                    return Results.BadRequest(new { error = "Undo command failed", message = undoResult.Message });
            }

            // Move back to active
            var entry = store.UnarchiveEntry(id);
            if (entry is null) return Results.BadRequest(new { error = "Failed to unarchive entry" });

            await hubContext.Clients.All.EntriesAdded(new List<Entry> { entry });
            return Results.Ok(entry);
        });

        // --- Dismiss entry ---
        group.MapPost("/{id}/dismiss",
            async (string id, EntryStore store, IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.ArchiveEntry(id, new EntryOutcome
            {
                Action = "Dismissed",
                Success = true,
                ResultMessage = "Entry dismissed by user"
            });

            if (entry is null) return Results.NotFound();

            await hubContext.Clients.All.EntryArchived(entry);
            return Results.Ok(entry);
        });

        // --- Batch dismiss ---
        group.MapPost("/batch/dismiss",
            async (BatchIdsRequest request, EntryStore store,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var dismissed = 0;
            foreach (var id in request.Ids)
            {
                var entry = store.ArchiveEntry(id, new EntryOutcome
                {
                    Action = "Dismissed",
                    Success = true,
                    ResultMessage = "Entry dismissed by user (batch)"
                });
                if (entry is not null)
                {
                    await hubContext.Clients.All.EntryArchived(entry);
                    dismissed++;
                }
            }
            return Results.Ok(new { dismissed, total = request.Ids.Count });
        });

        // --- Batch delete ---
        group.MapPost("/batch/delete",
            async (BatchIdsRequest request, EntryStore store,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var deleted = 0;
            foreach (var id in request.Ids)
            {
                if (store.DeleteEntry(id))
                {
                    await hubContext.Clients.All.EntryDeleted(id);
                    deleted++;
                }
            }
            return Results.Ok(new { deleted, total = request.Ids.Count });
        });

        // --- Batch action (execute same-named action on multiple entries) ---
        group.MapPost("/batch/action",
            async (BatchActionRequest request, EntryStore store, ActionExecutor executor,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var succeeded = 0;
            var failed = 0;

            foreach (var id in request.Ids)
            {
                var entry = store.GetEntry(id);
                if (entry is null) { failed++; continue; }

                var action = entry.Actions.FirstOrDefault(a =>
                    a.Label.Equals(request.ActionLabel, StringComparison.OrdinalIgnoreCase));
                if (action is null) { failed++; continue; }

                var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request.Parameters);
                if (errors.Count > 0) { failed++; continue; }

                var result = await executor.ExecuteAsync(action.Command, parameters);
                if (result.Success)
                {
                    switch (action.OnSuccess)
                    {
                        case PostActionBehavior.Archive:
                            var archived = store.ArchiveEntry(id, new EntryOutcome
                            {
                                Action = action.Label,
                                Success = true,
                                ResultMessage = result.Message
                            });
                            if (archived is not null)
                                await hubContext.Clients.All.EntryArchived(archived);
                            break;
                        case PostActionBehavior.Delete:
                            store.DeleteEntry(id);
                            await hubContext.Clients.All.EntryDeleted(id);
                            break;
                        case PostActionBehavior.Keep:
                            break;
                    }
                    succeeded++;
                }
                else
                {
                    failed++;
                }
            }

            return Results.Ok(new { succeeded, failed, total = request.Ids.Count });
        });

        // --- Delete single entry ---
        group.MapDelete("/{id}",
            async (string id, EntryStore store, IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var deleted = store.DeleteEntry(id);
            if (!deleted) return Results.NotFound();

            await hubContext.Clients.All.EntryDeleted(id);
            return Results.NoContent();
        });

        // --- Template endpoints ---
        var templateGroup = app.MapGroup("/api/templates");

        templateGroup.MapGet("/", (TemplateRegistry registry) =>
        {
            return Results.Ok(registry.GetAll());
        });

        templateGroup.MapGet("/auto-discovered", (AppConfig config) =>
        {
            var manifestPath = Path.Combine(config.DataDirectory, "templates", ".auto-discovered.json");
            if (!File.Exists(manifestPath))
                return Results.Ok(Array.Empty<string>());

            try
            {
                var json = File.ReadAllText(manifestPath);
                var types = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
                return Results.Ok(types);
            }
            catch
            {
                return Results.Ok(Array.Empty<string>());
            }
        });

        templateGroup.MapGet("/{type}", (string type, TemplateRegistry registry) =>
        {
            var template = registry.GetTemplate(type);
            return template is null ? Results.NotFound() : Results.Ok(template);
        });

        templateGroup.MapPost("/", (EntryTemplate template, TemplateRegistry registry) =>
        {
            try
            {
                registry.Register(template);
                return Results.Created($"/api/templates/{template.Type}", template);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        templateGroup.MapDelete("/{type}", (string type, TemplateRegistry registry) =>
        {
            return registry.Remove(type) ? Results.NoContent() : Results.NotFound();
        });
    }
}

// --- Request DTOs ---

public sealed class BatchIdsRequest
{
    public List<string> Ids { get; set; } = [];
}

public sealed class BatchActionRequest
{
    public List<string> Ids { get; set; } = [];
    public required string ActionLabel { get; set; }

    /// <summary>
    /// Parameter values applied to every entry's matching action. The same dict is validated
    /// per-entry against each action's declared parameters; entries whose action declares
    /// different parameters will fail validation and be counted as failed.
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Body of action-execution POSTs. Carries user-supplied parameter values that are validated
/// against the action's declared <c>parameters</c> before the command is executed.
/// </summary>
public sealed class ActionExecutionRequest
{
    public Dictionary<string, string>? Parameters { get; set; }
}

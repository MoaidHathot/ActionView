using System.Text.Json;
using System.Text.Json.Serialization;
using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.AspNetCore.SignalR;
using ActionView.Api.Hubs;

namespace ActionView.Api.Endpoints;

public static class EntryEndpoints
{
    // Matches the camelCase + enum conventions used across ActionView when binding
    // the raw request body for POST /api/entries.
    private static readonly JsonSerializerOptions EntryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

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
        // Accepts the raw JSON body so validation errors can be returned with precise
        // JSON paths (a bound Entry would fail model-binding before this code runs).
        group.MapPost("/", async (JsonElement body, EntryStore store, EntryValidator validator,
            AppConfig config, IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var raw = body.GetRawText();

            // Strict mode: block on any schema/normalization problem and return the report.
            if (config.Ingest.Strict)
            {
                var strictResult = validator.Validate(raw, new EntryValidationOptions { Strict = true });
                if (!strictResult.Ok)
                    return Results.BadRequest(new { error = "validation_failed", validation = strictResult });
            }

            Entry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<Entry>(raw, EntryJsonOptions);
            }
            catch (JsonException)
            {
                // Non-destructive default: only hard (unbindable) input is rejected, but with
                // a precise, structured reason instead of an opaque parser message.
                var report = validator.Validate(raw, new EntryValidationOptions { Strict = false });
                return Results.BadRequest(new { error = "validation_failed", validation = report });
            }

            if (entry is null)
                return Results.BadRequest(new { error = "Entry could not be parsed." });

            var ingested = store.IngestEntry(entry);
            if (ingested is null)
            {
                var report = validator.Validate(raw, new EntryValidationOptions { Strict = config.Ingest.Strict });
                if (!report.Ok)
                    return Results.BadRequest(new { error = "validation_failed", validation = report });
                return Results.BadRequest(new { error = "Invalid entry. Required fields: type, source, title." });
            }

            await hubContext.Clients.All.EntriesAdded(new List<Entry> { ingested });
            return Results.Created($"/api/entries/{ingested.Id}", ingested);
        });

        // --- Validate an entry without ingesting (POST /api/entries/validate) ---
        // The "retry oracle": submit best-effort JSON, get { ok, errors[], warnings[] } back,
        // fix, resubmit — no persistence, no side effects.
        group.MapPost("/validate", (JsonElement body, EntryValidator validator, AppConfig config,
            bool? strict, bool? includeNormalized) =>
        {
            var result = validator.Validate(body.GetRawText(), new EntryValidationOptions
            {
                Strict = strict ?? config.Ingest.Strict,
                IncludeNormalized = includeNormalized ?? false
            });

            return Results.Ok(result);
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

        // --- Execute entry action (starts a background job) ---
        // Returns 202 + the pending ActionJob immediately; progress/completion
        // arrive over SignalR (ActionJobStarted/Progress/Finished). Post-action
        // behavior (archive/delete) is applied on job success in Program.cs.
        group.MapPost("/{id}/actions/{actionIndex:int}",
            (string id, int actionIndex, ActionExecutionRequest? request,
             EntryStore store, ActionJobRunner jobs) =>
        {
            var entry = store.GetEntry(id);
            if (entry is null) return Results.NotFound(new { error = "Entry not found" });

            if (actionIndex < 0 || actionIndex >= entry.Actions.Count)
                return Results.BadRequest(new { error = "Invalid action index" });

            var action = entry.Actions[actionIndex];

            var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request?.Parameters);
            if (errors.Count > 0)
                return Results.BadRequest(new { error = "Invalid parameters", details = errors });

            var job = new ActionJob
            {
                EntryId = entry.Id,
                EntryTitle = entry.Title,
                ActionLabel = action.Label,
                ActionStyle = action.Style,
                Target = "entry",
                Command = ActionCommandInfo.From(action.Command),
                PostBehavior = action.OnSuccess,
                Trigger = "click",
            };
            jobs.Start(job, action.Command, parameters, new ActionContext { Entry = entry });
            return Results.Accepted($"/api/jobs/{job.Id}", job);
        });

        // --- Execute a block/section action addressed by positional path (job) ---
        // Path is a dot-delimited list of indices into the content/children tree
        // (e.g. "3.0" = entry.Content[3].Children[0]). Section actions never move
        // the entry (PostBehavior = Keep), preserving prior behavior. The owning
        // block is exposed to the command as {{content.self}}.
        group.MapPost("/{entryId}/blocks/{path}/actions/{actionIndex:int}",
            (string entryId, string path, int actionIndex, ActionExecutionRequest? request,
             EntryStore store, ActionJobRunner jobs) =>
        {
            var entry = store.GetEntry(entryId);
            if (entry is null) return Results.NotFound(new { error = "Entry not found" });

            var indices = BlockPath.Parse(path);
            if (indices is null) return Results.BadRequest(new { error = "Invalid block path" });

            var block = BlockPath.Resolve(entry, indices);
            if (block is null) return Results.BadRequest(new { error = "Block not found for path" });

            if (block.Actions is null || actionIndex < 0 || actionIndex >= block.Actions.Count)
                return Results.BadRequest(new { error = "Invalid action index" });

            var action = block.Actions[actionIndex];

            var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request?.Parameters);
            if (errors.Count > 0)
                return Results.BadRequest(new { error = "Invalid parameters", details = errors });

            var job = new ActionJob
            {
                EntryId = entry.Id,
                EntryTitle = entry.Title,
                ActionLabel = action.Label,
                ActionStyle = action.Style,
                Target = "section",
                Path = indices,
                TargetId = block.Id,
                Command = ActionCommandInfo.From(action.Command),
                PostBehavior = PostActionBehavior.Keep,
                Trigger = "click",
            };
            jobs.Start(job, action.Command, parameters, new ActionContext { Entry = entry, SelfBlock = block });
            return Results.Accepted($"/api/jobs/{job.Id}", job);
        });

        // --- Edit a block's text (persists to the entry; captures the original) ---
        group.MapPatch("/{entryId}/blocks/{path}",
            async (string entryId, string path, BlockEditRequest req, EntryStore store, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var indices = BlockPath.Parse(path);
            if (indices is null) return Results.BadRequest(new { error = "Invalid block path" });

            var check = store.GetEntry(entryId);
            var target = check is null ? null : BlockPath.Resolve(check, indices);
            if (check is null || target is null) return Results.NotFound(new { error = "Block not found" });

            var newValue = req.Value ?? string.Empty;
            string? original = null;
            string? blockId = target.Id;

            var entry = store.UpdateEntry(entryId, e =>
            {
                var block = BlockPath.Resolve(e, indices);
                if (block is null) return;
                var current = block.GetText();
                if (block.Edited is null)
                    block.Edited = new BlockEdit { OriginalText = current, FirstEditedAt = DateTimeOffset.UtcNow, LastEditedAt = DateTimeOffset.UtcNow, Count = 1 };
                else { block.Edited.LastEditedAt = DateTimeOffset.UtcNow; block.Edited.Count++; }
                original = block.Edited.OriginalText;
                block.Body = JsonSerializer.SerializeToElement(newValue);
            });
            if (entry is null) return Results.NotFound();

            audit.Append(new ActionEvent
            {
                EntryId = entryId,
                EntryTitle = entry.Title,
                ActionLabel = "Edited",
                Target = "content",
                Path = indices,
                TargetId = blockId,
                Trigger = "edit",
                Success = true,
                Message = "Block content edited",
                Output = Truncate($"--- original ---\n{original}\n--- edited ---\n{newValue}"),
            });

            await hubContext.Clients.All.EntryUpdated(entry);
            return Results.Ok(entry);
        });

        // --- Revert a block's text to the captured original ---
        group.MapPost("/{entryId}/blocks/{path}/revert",
            async (string entryId, string path, EntryStore store, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var indices = BlockPath.Parse(path);
            if (indices is null) return Results.BadRequest(new { error = "Invalid block path" });

            var check = store.GetEntry(entryId);
            var target = check is null ? null : BlockPath.Resolve(check, indices);
            if (check is null || target is null) return Results.NotFound(new { error = "Block not found" });
            if (target.Edited is null) return Results.BadRequest(new { error = "Block has no edit to revert" });

            var blockId = target.Id;
            var entry = store.UpdateEntry(entryId, e =>
            {
                var block = BlockPath.Resolve(e, indices);
                if (block?.Edited is null) return;
                block.Body = JsonSerializer.SerializeToElement(block.Edited.OriginalText);
                block.Edited = null;
            });
            if (entry is null) return Results.NotFound();

            audit.Append(new ActionEvent
            {
                EntryId = entryId,
                EntryTitle = entry.Title,
                ActionLabel = "Reverted",
                Target = "content",
                Path = indices,
                TargetId = blockId,
                Trigger = "edit",
                Success = true,
                Message = "Block content reverted to original",
            });

            await hubContext.Clients.All.EntryUpdated(entry);
            return Results.Ok(entry);
        });

        // --- Per-entry action history (audit log) ---
        // Survives archive/dismiss/delete: the log is keyed by entry id and is
        // never pruned when the entry moves or is removed.
        group.MapGet("/{id}/history", (string id, ActionAuditLog audit, int? limit) =>
            Results.Ok(audit.GetForEntry(id, limit ?? 200)));

        // --- Action jobs (background execution status/cancel) ---
        var jobsGroup = app.MapGroup("/api/jobs");
        jobsGroup.MapGet("/", (ActionJobRunner jobs, string? entryId) => Results.Ok(jobs.Active(entryId)));
        jobsGroup.MapGet("/{jobId}", (string jobId, ActionJobRunner jobs) =>
        {
            var job = jobs.Get(jobId);
            return job is null ? Results.NotFound() : Results.Ok(job);
        });
        jobsGroup.MapPost("/{jobId}/cancel", (string jobId, ActionJobRunner jobs) =>
            jobs.Cancel(jobId) ? Results.Ok(new { cancelled = true }) : Results.NotFound());


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
                   EntryStore store, ActionExecutor executor, ActionAuditLog audit,
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

                var undoResult = await executor.ExecuteAsync(undoCommand, parameters, new ActionContext { Entry = archivedEntry });
                audit.Append(new ActionEvent
                {
                    EntryId = id,
                    EntryTitle = archivedEntry.Title,
                    ActionLabel = $"Undo: {action?.Label ?? archivedEntry.Outcome?.Action ?? "action"}",
                    ActionStyle = action?.Style ?? ActionStyle.Default,
                    Target = "system",
                    Trigger = "undo",
                    Command = ActionCommandInfo.From(undoCommand),
                    Success = undoResult.Success,
                    StatusCode = undoResult.StatusCode,
                    Message = undoResult.Message,
                    Output = undoResult.Output,
                });
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
            async (string id, EntryStore store, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.ArchiveEntry(id, new EntryOutcome
            {
                Action = "Dismissed",
                Success = true,
                ResultMessage = "Entry dismissed by user"
            });

            if (entry is null) return Results.NotFound();

            audit.Append(SystemEvent(entry, "Dismissed", "dismiss", PostActionBehavior.Archive));
            await hubContext.Clients.All.EntryArchived(entry);
            return Results.Ok(entry);
        });

        // --- Batch dismiss ---
        group.MapPost("/batch/dismiss",
            async (BatchIdsRequest request, EntryStore store, ActionAuditLog audit,
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
                    audit.Append(SystemEvent(entry, "Dismissed", "batch", PostActionBehavior.Archive));
                    await hubContext.Clients.All.EntryArchived(entry);
                    dismissed++;
                }
            }
            return Results.Ok(new { dismissed, total = request.Ids.Count });
        });

        // --- Batch delete ---
        group.MapPost("/batch/delete",
            async (BatchIdsRequest request, EntryStore store, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var deleted = 0;
            foreach (var id in request.Ids)
            {
                var entry = store.GetEntry(id);
                if (store.DeleteEntry(id))
                {
                    audit.Append(SystemEvent(entry, "Deleted", "batch", PostActionBehavior.Delete, entryId: id));
                    await hubContext.Clients.All.EntryDeleted(id);
                    deleted++;
                }
            }
            return Results.Ok(new { deleted, total = request.Ids.Count });
        });

        // --- Batch action (execute same-named action on multiple entries) ---
        group.MapPost("/batch/action",
            async (BatchActionRequest request, EntryStore store, ActionExecutor executor, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var succeeded = 0;
            var failed = 0;

            foreach (var id in request.Ids)
            {
                var entry = store.GetEntry(id);
                if (entry is null) { failed++; continue; }

                var actionIdx = entry.Actions.FindIndex(a =>
                    a.Label.Equals(request.ActionLabel, StringComparison.OrdinalIgnoreCase));
                if (actionIdx < 0) { failed++; continue; }
                var action = entry.Actions[actionIdx];

                var (errors, parameters) = ActionParameterValidator.Validate(action.Parameters, request.Parameters);
                if (errors.Count > 0) { failed++; continue; }

                var result = await executor.ExecuteAsync(action.Command, parameters, new ActionContext { Entry = entry });
                var post = result.Success ? action.OnSuccess : (PostActionBehavior?)null;
                audit.Append(BuildActionEvent(entry, action, "entry", path: null, targetId: null, trigger: "batch", result, post));

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
            async (string id, EntryStore store, ActionAuditLog audit,
                   IHubContext<EntryHub, IEntryHubClient> hubContext) =>
        {
            var entry = store.GetEntry(id);
            var deleted = store.DeleteEntry(id);
            if (!deleted) return Results.NotFound();

            audit.Append(SystemEvent(entry, "Deleted", "click", PostActionBehavior.Delete, entryId: id));
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

    // --- Audit + block-path helpers ---

    /// <summary>Truncates long audit payloads (before/after diffs, output) to keep the log compact.</summary>
    private static string Truncate(string value, int max = 2000)
        => value.Length > max ? value[..max] + "..." : value;

    private static ActionEvent BuildActionEvent(
        Entry entry, EntryAction action, string target, List<int>? path, string? targetId,
        string trigger, ActionExecutionResult result, PostActionBehavior? post) => new()
    {
        EntryId = entry.Id,
        EntryTitle = entry.Title,
        ActionLabel = action.Label,
        ActionStyle = action.Style,
        Target = target,
        Path = path,
        TargetId = targetId,
        Trigger = trigger,
        Command = ActionCommandInfo.From(action.Command),
        Success = result.Success,
        StatusCode = result.StatusCode,
        Message = result.Message,
        Output = result.Output,
        PostBehavior = post,
    };

    private static ActionEvent SystemEvent(
        Entry? entry, string label, string trigger, PostActionBehavior? post, string? entryId = null) => new()
    {
        EntryId = entry?.Id ?? entryId ?? string.Empty,
        EntryTitle = entry?.Title,
        ActionLabel = label,
        ActionStyle = ActionStyle.Default,
        Target = "system",
        Trigger = trigger,
        Success = true,
        PostBehavior = post,
    };
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

/// <summary>Body of a block-edit PATCH: the new text for the block.</summary>
public sealed class BlockEditRequest
{
    public string? Value { get; set; }
}

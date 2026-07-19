using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/history");

        group.MapGet("/", (EntryStore store, AppConfig config,
            string? type, string? severity, string? source, string? tags, string? search,
            string? tagMode, string? sort, string? dir, int? limit, int? offset) =>
        {
            var criteria = EntryFiltering.ParseCriteria(
                type, severity, source, tags, tagMode, search,
                config.TagMatchMode, includeOutcomeInSearch: true);

            var entries = store.GetArchivedEntries(
                criteria, limit ?? 50, offset ?? 0,
                EntrySorting.TryParseField(sort), EntrySorting.ParseDirection(dir));

            return Results.Ok(entries);
        });

        group.MapGet("/{id}", (string id, EntryStore store) =>
        {
            var entry = store.GetArchivedEntry(id);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });

        // Global action activity feed (newest first) across all entries.
        group.MapGet("/actions", (ActionAuditLog audit, int? limit) =>
            Results.Ok(audit.GetRecent(limit ?? 200)));
    }
}

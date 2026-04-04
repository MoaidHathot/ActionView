using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/history");

        group.MapGet("/", (EntryStore store, string? type, string? severity, string? source, string? tags, string? search, int? limit, int? offset) =>
        {
            var entries = store.GetArchivedEntries(type, limit ?? 50, offset ?? 0);

            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Severity>(severity, true, out var sev))
                entries = entries.Where(e => e.Severity == sev).ToList();

            if (!string.IsNullOrWhiteSpace(source))
                entries = entries.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                entries = entries.Where(e => tagList.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries.Where(e =>
                    e.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.Subtitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    e.Source.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    e.Type.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    e.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Outcome?.Action.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            return Results.Ok(entries);
        });

        group.MapGet("/{id}", (string id, EntryStore store) =>
        {
            var entry = store.GetArchivedEntry(id);
            return entry is null ? Results.NotFound() : Results.Ok(entry);
        });
    }
}

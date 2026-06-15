using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Api.Endpoints;

public static class ViewEndpoints
{
    public static void MapViewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/views");

        // --- List saved views (excludes the synthetic "All" view) ---
        group.MapGet("/", (ViewStore store) => Results.Ok(store.GetViews()));

        // --- Replace the full set of saved views and persist to actionview.json ---
        group.MapPut("/", (List<SavedView> views, ViewStore store, ILogger<ViewStore> logger) =>
        {
            try
            {
                var saved = store.SaveViews(views ?? []);
                return Results.Ok(saved);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist views.");
                return Results.Problem("Failed to persist views: " + ex.Message);
            }
        });

        // --- Active-entry counts per view (drives the pill badges) ---
        group.MapGet("/counts", (ViewStore store, EntryStore entryStore, AppConfig config) =>
        {
            var active = entryStore.GetActiveEntries();
            var counts = new Dictionary<string, int>();
            foreach (var view in store.GetViews())
            {
                var criteria = EntryFiltering.CriteriaForView(view, config.TagMatchMode);
                counts[view.Id] = EntryFiltering.Apply(active, criteria).Count();
            }

            return Results.Ok(new ViewCountsResponse { All = active.Count, Counts = counts });
        });
    }
}

/// <summary>Active-entry counts: total plus a per-view breakdown keyed by view id.</summary>
public sealed class ViewCountsResponse
{
    public int All { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
}

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
    }
}

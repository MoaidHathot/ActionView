using ActionView.Core.Services;

namespace ActionView.Api.Endpoints;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/stats", (EntryStore store) =>
        {
            var stats = store.GetStats();
            return Results.Ok(stats);
        });
    }
}

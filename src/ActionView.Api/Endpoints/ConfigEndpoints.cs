using ActionView.Core.Models;

namespace ActionView.Api.Endpoints;

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this WebApplication app)
    {
        // Exposes the small subset of server config the dashboard needs to
        // mirror defaults (e.g. the global tag-match mode for the AND/OR toggle).
        app.MapGet("/api/config", (AppConfig config) => Results.Ok(new ClientConfig
        {
            TagMatchMode = config.TagMatchMode,
        }));
    }
}

/// <summary>Client-facing slice of <see cref="AppConfig"/>.</summary>
public sealed class ClientConfig
{
    public TagMatchMode TagMatchMode { get; set; }
}

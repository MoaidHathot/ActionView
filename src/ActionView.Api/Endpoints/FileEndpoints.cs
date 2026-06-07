using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.AspNetCore.Http;

namespace ActionView.Api.Endpoints;

/// <summary>
/// Endpoint that serves local files referenced by entries.
///
/// Browsers refuse to load <c>file://</c> URLs from an <c>http://</c> origin,
/// so entries that want to embed local images go through here instead:
/// the client rewrites <c>file:///C:/path/to/foo.jpg</c> to
/// <c>/api/files?path=C%3A%2Fpath%2Fto%2Ffoo.jpg</c>.
///
/// All access is gated by <see cref="FileAccessResolver"/>, which enforces
/// the allowlist configured in <c>actionview.json</c>. See
/// <see cref="FileAccessConfig"/> for the security model.
/// </summary>
public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        app.MapGet("/api/files", (string? path, FileAccessResolver resolver, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ActionView.Api.Endpoints.FileEndpoints");
            var result = resolver.TryResolve(path, out var canonicalPath);
            return result switch
            {
                FileAccessResult.Allowed => ServeFile(canonicalPath),
                FileAccessResult.InvalidPath => Results.BadRequest(new
                {
                    error = "Invalid path. Provide an absolute file path or a file:// URL.",
                }),
                FileAccessResult.NotAllowed => LogAndForbid(logger, path,
                    "Path is not under any configured fileAccess.allowedRoots."),
                FileAccessResult.NotAFile => Results.BadRequest(new
                {
                    error = "Path refers to a directory, not a file.",
                }),
                FileAccessResult.NotFound => Results.NotFound(new
                {
                    error = "File not found.",
                }),
                FileAccessResult.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        });
    }

    private static IResult ServeFile(string canonicalPath)
    {
        // Stream the file rather than buffering it; Results.File takes care of
        // ETag/Last-Modified/Range so range requests for video files work.
        var contentType = FileAccessResolver.GuessContentType(canonicalPath);
        var fileName = Path.GetFileName(canonicalPath);
        var stream = File.OpenRead(canonicalPath);
        return Results.File(stream, contentType, fileName, enableRangeProcessing: true,
            lastModified: File.GetLastWriteTimeUtc(canonicalPath));
    }

    private static IResult LogAndForbid(ILogger logger, string? path, string reason)
    {
        // Log at Information so an operator who's wondering why an image
        // doesn't load can see the rejection and the requested path.
        logger.LogInformation("Refused /api/files request for {Path}: {Reason}", path, reason);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}

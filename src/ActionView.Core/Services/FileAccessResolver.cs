using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Resolves and validates local-file-system paths against the configured
/// <see cref="FileAccessConfig.AllowedRoots"/> allowlist.
///
/// This is the single gatekeeper for the <c>/api/files</c> endpoint
/// (and any other surface that wants to expose host-local files to the
/// browser). Pulling the logic into a service makes it unit-testable
/// without spinning up an HTTP host.
///
/// Safety properties:
/// <list type="bullet">
///   <item>An empty <see cref="FileAccessConfig.AllowedRoots"/> list denies everything.</item>
///   <item>Requested paths must be absolute (rooted).</item>
///   <item>The path is canonicalised with <see cref="Path.GetFullPath(string)"/> so
///         <c>..</c> traversal is collapsed before the check.</item>
///   <item>If the target file is a symbolic link, its resolved target must
///         also lie underneath an allowed root. This blocks symlink escape.</item>
///   <item>Only regular files are served (directories return <see cref="FileAccessResult.NotAFile"/>).</item>
///   <item>Files larger than <see cref="FileAccessConfig.MaxFileSizeBytes"/> are
///         reported as <see cref="FileAccessResult.TooLarge"/>; the caller decides
///         how to surface that (typically HTTP 413).</item>
/// </list>
/// </summary>
public sealed class FileAccessResolver
{
    private readonly FileAccessConfig _config;
    private readonly IReadOnlyList<string> _normalizedRoots;

    public FileAccessResolver(AppConfig appConfig)
        : this(appConfig.FileAccess) { }

    public FileAccessResolver(FileAccessConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        // Pre-normalise the allowed roots once. We always append a trailing
        // directory separator so the prefix check cannot accept a sibling
        // path that merely shares a prefix string (e.g. allowed root
        // "C:\\data" must not match "C:\\data-evil\\file.png").
        _normalizedRoots = (_config.AllowedRoots ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizeDirectory)
            .ToArray();
    }

    /// <summary>Allowed roots in canonical form, each ending with a directory separator.</summary>
    public IReadOnlyList<string> AllowedRoots => _normalizedRoots;

    /// <summary>
    /// Attempts to resolve <paramref name="requestedPath"/> to a file that may be served.
    /// Returns <see cref="FileAccessResult.Allowed"/> with <paramref name="canonicalPath"/>
    /// set to the canonical on-disk path on success.
    /// </summary>
    /// <param name="requestedPath">
    /// Either a plain absolute path (<c>C:\\foo\\bar.jpg</c>, <c>/var/foo/bar.jpg</c>)
    /// or a <c>file://</c> URI. URL-encoded characters in URIs are decoded by
    /// <see cref="Uri.LocalPath"/>.
    /// </param>
    public FileAccessResult TryResolve(string? requestedPath, out string canonicalPath)
    {
        canonicalPath = string.Empty;

        if (string.IsNullOrWhiteSpace(requestedPath))
            return FileAccessResult.InvalidPath;

        // Accept file:// URIs by extracting their LocalPath. We don't accept
        // any other scheme — http/https/data: URIs are the client's problem.
        var pathString = requestedPath;
        if (pathString.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(pathString, UriKind.Absolute, out var uri) || !uri.IsFile)
                return FileAccessResult.InvalidPath;
            pathString = uri.LocalPath;
        }

        if (!Path.IsPathRooted(pathString))
            return FileAccessResult.InvalidPath;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(pathString);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return FileAccessResult.InvalidPath;
        }

        if (_normalizedRoots.Count == 0)
            return FileAccessResult.NotAllowed;

        if (!IsUnderAllowedRoot(fullPath))
            return FileAccessResult.NotAllowed;

        FileInfo info;
        try
        {
            info = new FileInfo(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return FileAccessResult.InvalidPath;
        }

        if (!info.Exists)
        {
            // Distinguish a missing file from a directory so callers can return 404 vs 400.
            if (Directory.Exists(fullPath))
                return FileAccessResult.NotAFile;
            return FileAccessResult.NotFound;
        }

        // If this is a symlink (or junction), make sure the resolved target also
        // lies underneath an allowed root. Without this, an attacker who can
        // place a symlink inside an allowed root could read arbitrary files.
        var linkTarget = info.ResolveLinkTarget(returnFinalTarget: true);
        if (linkTarget is not null)
        {
            string targetFull;
            try
            {
                targetFull = Path.GetFullPath(linkTarget.FullName);
            }
            catch
            {
                return FileAccessResult.NotAllowed;
            }
            if (!IsUnderAllowedRoot(targetFull))
                return FileAccessResult.NotAllowed;
            fullPath = targetFull;
        }

        if (info.Length > _config.MaxFileSizeBytes)
            return FileAccessResult.TooLarge;

        canonicalPath = fullPath;
        return FileAccessResult.Allowed;
    }

    /// <summary>
    /// Returns a best-effort Content-Type string for a path, based on extension.
    /// Returns <c>application/octet-stream</c> for anything unknown.
    /// </summary>
    public static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".avif" => "image/avif",
            ".ico" => "image/x-icon",
            ".tif" or ".tiff" => "image/tiff",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".pdf" => "application/pdf",
            ".txt" or ".log" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    private bool IsUnderAllowedRoot(string fullPath)
    {
        // Path comparison on Windows is case-insensitive; on Unix it's case-sensitive.
        var cmp = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var root in _normalizedRoots)
        {
            // The path itself, when treated as a directory candidate, must also
            // end with a separator so "C:\\data" cannot match "C:\\data-evil".
            // For a file path, appending the separator only at the root side and
            // requiring StartsWith covers both files and nested directories.
            if (fullPath.StartsWith(root, cmp))
                return true;

            // Edge case: an allowed root that points at a single file (rare but
            // not invalid) — exact match is allowed.
            var rootNoSep = TrimTrailingSeparator(root);
            if (string.Equals(fullPath, rootNoSep, cmp))
                return true;
        }
        return false;
    }

    private static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length == 0) return full;
        var sep = Path.DirectorySeparatorChar;
        if (full[^1] != sep && full[^1] != Path.AltDirectorySeparatorChar)
            full += sep;
        return full;
    }

    private static string TrimTrailingSeparator(string path)
    {
        if (path.Length == 0) return path;
        var last = path[^1];
        if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
            return path[..^1];
        return path;
    }
}

/// <summary>Outcome of a <see cref="FileAccessResolver.TryResolve(string, out string)"/> call.</summary>
public enum FileAccessResult
{
    /// <summary>Path is valid and may be served.</summary>
    Allowed,

    /// <summary>Path was null/empty/relative/malformed.</summary>
    InvalidPath,

    /// <summary>Path is well-formed but not under any allowed root (or no allowlist is configured).</summary>
    NotAllowed,

    /// <summary>Path is allowed but no file exists at that location.</summary>
    NotFound,

    /// <summary>Path resolves to a directory rather than a file.</summary>
    NotAFile,

    /// <summary>File exists and is allowed, but exceeds the configured size limit.</summary>
    TooLarge,
}

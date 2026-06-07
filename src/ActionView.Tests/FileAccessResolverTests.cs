using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

/// <summary>
/// Tests for <see cref="FileAccessResolver"/> — the gatekeeper that decides
/// which local files /api/files is allowed to serve.
///
/// These tests exercise the resolver directly (no HTTP host needed) and
/// focus on the security-relevant edge cases: empty allowlist, traversal,
/// sibling-prefix attacks, symlink escape, size limits.
/// </summary>
public sealed class FileAccessResolverTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _allowedDir;
    private readonly string _outsideDir;

    public FileAccessResolverTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"av_fileaccess_{Guid.NewGuid():N}");
        _allowedDir = Path.Combine(_tempRoot, "allowed");
        _outsideDir = Path.Combine(_tempRoot, "outside");
        Directory.CreateDirectory(_allowedDir);
        Directory.CreateDirectory(_outsideDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private FileAccessResolver Resolver(params string[] roots) =>
        new(new FileAccessConfig { AllowedRoots = roots.ToList() });

    private string WriteFile(string dir, string name, string contents = "x")
    {
        var path = Path.Combine(dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    // ----- Allowlist gating -----

    [Fact]
    public void EmptyAllowlist_DeniesEvenExistingFiles()
    {
        var file = WriteFile(_allowedDir, "ok.txt");
        var resolver = Resolver();

        var result = resolver.TryResolve(file, out var canonical);

        Assert.Equal(FileAccessResult.NotAllowed, result);
        Assert.Equal(string.Empty, canonical);
    }

    [Fact]
    public void FileUnderAllowedRoot_IsAllowed()
    {
        var file = WriteFile(_allowedDir, "ok.txt");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(file, out var canonical);

        Assert.Equal(FileAccessResult.Allowed, result);
        Assert.Equal(Path.GetFullPath(file), canonical);
    }

    [Fact]
    public void FileInNestedSubdirectory_IsAllowed()
    {
        var file = WriteFile(Path.Combine(_allowedDir, "sub", "deeper"), "frame.jpg");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(file, out _);

        Assert.Equal(FileAccessResult.Allowed, result);
    }

    [Fact]
    public void FileOutsideAllowedRoot_IsRejected()
    {
        var file = WriteFile(_outsideDir, "leak.txt");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(file, out _);

        Assert.Equal(FileAccessResult.NotAllowed, result);
    }

    // ----- Traversal & sibling-prefix attacks -----

    [Fact]
    public void TraversalThatExitsAllowedRoot_IsRejected()
    {
        var outsideFile = WriteFile(_outsideDir, "leak.txt");
        // Build a path that *looks like* it's under the allowed root but
        // canonicalises out of it via ".." segments.
        var sneaky = Path.Combine(_allowedDir, "..", "outside", "leak.txt");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(sneaky, out _);

        Assert.Equal(FileAccessResult.NotAllowed, result);
        // Sanity check: that path really does refer to the outside file.
        Assert.Equal(Path.GetFullPath(outsideFile), Path.GetFullPath(sneaky));
    }

    [Fact]
    public void SiblingDirectoryThatSharesPrefix_IsRejected()
    {
        // "allowed-evil" shares the prefix "allowed" with the allowed root.
        // A naive StartsWith check would accept it; the resolver must not.
        var evilDir = Path.Combine(_tempRoot, "allowed-evil");
        Directory.CreateDirectory(evilDir);
        var evilFile = WriteFile(evilDir, "leak.txt");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(evilFile, out _);

        Assert.Equal(FileAccessResult.NotAllowed, result);
    }

    // ----- file:// URI handling -----

    [Fact]
    public void FileUri_IsAccepted()
    {
        var file = WriteFile(_allowedDir, "image.jpg");
        var uri = new Uri(file).AbsoluteUri; // e.g. file:///C:/temp/.../image.jpg
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(uri, out var canonical);

        Assert.Equal(FileAccessResult.Allowed, result);
        Assert.Equal(Path.GetFullPath(file), canonical);
    }

    [Fact]
    public void PercentEncodedFileUri_IsDecoded()
    {
        // Directory and filename with a space — must survive URI encoding.
        var dir = Path.Combine(_allowedDir, "my pics");
        var file = WriteFile(dir, "shot one.jpg");
        var uri = new Uri(file).AbsoluteUri;
        Assert.Contains("%20", uri);

        var resolver = Resolver(_allowedDir);
        var result = resolver.TryResolve(uri, out var canonical);

        Assert.Equal(FileAccessResult.Allowed, result);
        Assert.Equal(Path.GetFullPath(file), canonical);
    }

    [Fact]
    public void NonFileScheme_IsRejected()
    {
        var resolver = Resolver(_allowedDir);

        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("http://example.com/x.jpg", out _));
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("https://example.com/x.jpg", out _));
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("data:image/png;base64,AAA=", out _));
    }

    // ----- Input validation -----

    [Fact]
    public void NullOrWhitespace_IsInvalid()
    {
        var resolver = Resolver(_allowedDir);
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve(null, out _));
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve(string.Empty, out _));
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("   ", out _));
    }

    [Fact]
    public void RelativePath_IsRejected()
    {
        var resolver = Resolver(_allowedDir);
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("relative/path.jpg", out _));
        Assert.Equal(FileAccessResult.InvalidPath, resolver.TryResolve("./foo.jpg", out _));
    }

    [Fact]
    public void NonexistentFileUnderAllowedRoot_IsNotFound()
    {
        var phantom = Path.Combine(_allowedDir, "no-such-image.png");
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(phantom, out _);

        Assert.Equal(FileAccessResult.NotFound, result);
    }

    [Fact]
    public void DirectoryPath_IsNotAFile()
    {
        var subdir = Path.Combine(_allowedDir, "subdir");
        Directory.CreateDirectory(subdir);
        var resolver = Resolver(_allowedDir);

        var result = resolver.TryResolve(subdir, out _);

        Assert.Equal(FileAccessResult.NotAFile, result);
    }

    // ----- Size limit -----

    [Fact]
    public void FileLargerThanLimit_IsTooLarge()
    {
        var file = WriteFile(_allowedDir, "big.bin", new string('x', 1024));
        var resolver = new FileAccessResolver(new FileAccessConfig
        {
            AllowedRoots = [_allowedDir],
            MaxFileSizeBytes = 100,
        });

        var result = resolver.TryResolve(file, out _);

        Assert.Equal(FileAccessResult.TooLarge, result);
    }

    // ----- Symlink escape -----

    /// <summary>
    /// Attempts to create a symbolic link, returning false (and swallowing the
    /// failure) on platforms / sessions where we don't have the privilege.
    /// On Windows this requires Developer Mode or admin; on Unix it's free.
    /// </summary>
    private static bool TryCreateSymbolicLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (PlatformNotSupportedException) { return false; }
        catch (IOException) { return false; } // covers "required privilege" on Windows
    }

    [Fact]
    public void SymlinkInsideAllowedRoot_ButTargetOutside_IsRejected()
    {
        // Set up: real file outside the allowed root, plus a symlink to it
        // *inside* the allowed root. Without symlink resolution this would
        // pass the prefix check; the resolver must reject it anyway.
        var realFile = WriteFile(_outsideDir, "secret.txt", "leaked");
        var link = Path.Combine(_allowedDir, "link-to-secret.txt");

        if (!TryCreateSymbolicLink(link, realFile))
            return; // Skip: can't create symlinks in this environment.

        var resolver = Resolver(_allowedDir);
        var result = resolver.TryResolve(link, out _);

        Assert.Equal(FileAccessResult.NotAllowed, result);
    }

    [Fact]
    public void SymlinkInsideAllowedRoot_TargetAlsoInside_IsAllowed()
    {
        var realFile = WriteFile(_allowedDir, "real.txt");
        var link = Path.Combine(_allowedDir, "alias.txt");

        if (!TryCreateSymbolicLink(link, realFile))
            return; // Skip: can't create symlinks in this environment.

        var resolver = Resolver(_allowedDir);
        var result = resolver.TryResolve(link, out var canonical);

        Assert.Equal(FileAccessResult.Allowed, result);
        Assert.Equal(Path.GetFullPath(realFile), canonical);
    }

    // ----- Multiple allowed roots -----

    [Fact]
    public void MultipleAllowedRoots_AnyRootIsSufficient()
    {
        var secondRoot = Path.Combine(_tempRoot, "second");
        Directory.CreateDirectory(secondRoot);
        var fileA = WriteFile(_allowedDir, "a.txt");
        var fileB = WriteFile(secondRoot, "b.txt");

        var resolver = Resolver(_allowedDir, secondRoot);

        Assert.Equal(FileAccessResult.Allowed, resolver.TryResolve(fileA, out _));
        Assert.Equal(FileAccessResult.Allowed, resolver.TryResolve(fileB, out _));
    }

    // ----- Content-type sniffing -----

    [Theory]
    [InlineData("image.png", "image/png")]
    [InlineData("image.jpg", "image/jpeg")]
    [InlineData("image.JPEG", "image/jpeg")]
    [InlineData("image.gif", "image/gif")]
    [InlineData("image.webp", "image/webp")]
    [InlineData("image.svg", "image/svg+xml")]
    [InlineData("clip.mp4", "video/mp4")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("notes.txt", "text/plain; charset=utf-8")]
    [InlineData("mystery.xyz", "application/octet-stream")]
    public void GuessContentType_ReturnsExpectedMime(string fileName, string expected)
    {
        Assert.Equal(expected, FileAccessResolver.GuessContentType(fileName));
    }
}

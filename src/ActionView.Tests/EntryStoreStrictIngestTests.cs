using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

/// <summary>
/// Tests for opt-in strict ingest and the improved error messages on the file-drop path.
/// The non-destructive default must keep shipping entries that ship today; strict must
/// reject imperfect entries with a precise, structured reason in errors/.
/// </summary>
public class EntryStoreStrictIngestTests : IDisposable
{
    private readonly string _tempDir;

    public EntryStoreStrictIngestTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_strict_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private EntryStore CreateStore(bool strict, params EntryTemplate[] templates)
    {
        var registry = new TemplateRegistry(_tempDir, NullLogger<TemplateRegistry>.Instance);
        foreach (var template in templates)
            registry.Register(template);
        var normalizer = new EntryNormalizer(registry, NullLogger<EntryNormalizer>.Instance);
        var validator = new EntryValidator(normalizer);
        return new EntryStore(_tempDir, NullLogger<EntryStore>.Instance, normalizer, validator, strictIngest: strict);
    }

    private static EntryTemplate PrTemplate(bool strict = false) => new()
    {
        Type = "pr",
        Strict = strict,
        ContentTemplate =
        [
            new ContentTemplateBlock { Type = ContentBlockType.Markdown, Label = "Summary", Required = true }
        ]
    };

    private string DropInbox(string json)
    {
        var name = $"{Guid.NewGuid():N}.json";
        var path = Path.Combine(_tempDir, "inbox", name);
        File.WriteAllText(path, json);
        return path;
    }

    private string ErrorCompanionFor(string inboxPath)
        => Path.Combine(_tempDir, "errors", Path.GetFileNameWithoutExtension(inboxPath) + ".error.txt");

    [Fact]
    public void StrictIngest_MissingRequiredBlock_MovedToErrorsWithReason()
    {
        var store = CreateStore(strict: true, PrTemplate());
        var path = DropInbox("""{"type":"pr","source":"s","title":"Hi"}""");

        var result = store.PickupInboxFile(path);

        Assert.Null(result);
        Assert.False(File.Exists(path)); // removed from inbox
        var companion = ErrorCompanionFor(path);
        Assert.True(File.Exists(companion));
        Assert.Contains("block.missingRequired", File.ReadAllText(companion));
    }

    [Fact]
    public void NonStrictIngest_MissingRequiredBlock_StillShips()
    {
        var store = CreateStore(strict: false, PrTemplate());
        var path = DropInbox("""{"type":"pr","source":"s","title":"Hi"}""");

        var result = store.PickupInboxFile(path);

        Assert.NotNull(result); // non-destructive default: entry ships
        Assert.NotNull(store.GetEntry(result!.Id));
    }

    [Fact]
    public void TemplateStrict_MissingRequiredBlock_MovedToErrorsEvenWhenGlobalNonStrict()
    {
        var store = CreateStore(strict: false, PrTemplate(strict: true));
        var path = DropInbox("""{"type":"pr","source":"s","title":"Hi"}""");

        var result = store.PickupInboxFile(path);

        Assert.Null(result);
        Assert.True(File.Exists(ErrorCompanionFor(path)));
    }

    [Fact]
    public void NonStrictIngest_BadEnum_MovedToErrorsWithPreciseSchemaMessage()
    {
        var store = CreateStore(strict: false);
        var path = DropInbox("""{"type":"t","source":"s","title":"Hi","severity":"urgent"}""");

        var result = store.PickupInboxFile(path);

        Assert.Null(result);
        var companion = ErrorCompanionFor(path);
        Assert.True(File.Exists(companion));
        Assert.Contains("schema.enum", File.ReadAllText(companion));
    }

    [Fact]
    public void StrictIngest_ValidEntry_IsAccepted()
    {
        var store = CreateStore(strict: true, PrTemplate());
        var path = DropInbox(
            """{"type":"pr","source":"s","title":"Hi","content":[{"type":"markdown","label":"Summary","body":"ok"}]}""");

        var result = store.PickupInboxFile(path);

        Assert.NotNull(result);
        Assert.NotNull(store.GetEntry(result!.Id));
    }
}

using ActionView.Core.Models;
using ActionView.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionView.Tests;

/// <summary>
/// Tests for the shared validation pipeline (the "retry oracle"): schema validation,
/// template normalization findings, strict promotion, tag normalization, and payload bounds.
/// </summary>
public class EntryValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public EntryValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"actionview_validator_{Guid.NewGuid():N}");
        ConfigLoader.EnsureDirectories(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private EntryValidator CreateValidator(params EntryTemplate[] templates)
    {
        var registry = new TemplateRegistry(_tempDir, NullLogger<TemplateRegistry>.Instance);
        foreach (var template in templates)
            registry.Register(template);
        var normalizer = new EntryNormalizer(registry, NullLogger<EntryNormalizer>.Instance);
        return new EntryValidator(normalizer);
    }

    private static EntryTemplate PrTemplateWithRequiredSummary(bool strict = false) => new()
    {
        Type = "pr",
        Strict = strict,
        ContentTemplate =
        [
            new ContentTemplateBlock
            {
                Type = ContentBlockType.Markdown,
                Label = "Summary",
                Required = true
            }
        ]
    };

    [Fact]
    public void Validate_ValidMinimalEntry_ReturnsOk()
    {
        var result = CreateValidator().Validate("""{"type":"t","source":"s","title":"Hi"}""");

        Assert.True(result.Ok);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsParseError()
    {
        var result = CreateValidator().Validate("not json");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "json.parse");
    }

    [Fact]
    public void Validate_BadEnum_ReturnsSchemaEnumErrorWithPath()
    {
        var result = CreateValidator().Validate(
            """{"type":"t","source":"s","title":"Hi","severity":"urgent"}""");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "schema.enum" && e.Path == "/severity");
    }

    [Fact]
    public void Validate_MissingRequiredTopLevelField_ReturnsSchemaRequired()
    {
        var result = CreateValidator().Validate("""{"source":"s","title":"Hi"}""");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "schema.required");
    }

    [Fact]
    public void Validate_EmptyRequiredField_ReturnsMinLengthWithPath()
    {
        var result = CreateValidator().Validate("""{"type":"t","source":"","title":"Hi"}""");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "schema.minLength" && e.Path == "/source");
    }

    [Fact]
    public void Validate_NestedBlockMissingType_ReportsPreciseInstancePath()
    {
        var result = CreateValidator().Validate(
            """{"type":"t","source":"s","title":"Hi","content":[{"label":"x"}]}""");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "schema.required" && e.Path == "/content/0");
    }

    [Fact]
    public void Validate_MissingRequiredBlock_IsWarningInNonStrict()
    {
        var validator = CreateValidator(PrTemplateWithRequiredSummary());

        var result = validator.Validate("""{"type":"pr","source":"s","title":"Hi"}""");

        Assert.True(result.Ok); // non-strict: warnings do not block
        Assert.Contains(result.Warnings, w => w.Code == "block.missingRequired");
    }

    [Fact]
    public void Validate_MissingRequiredBlock_IsErrorInStrict()
    {
        var validator = CreateValidator(PrTemplateWithRequiredSummary());

        var result = validator.Validate(
            """{"type":"pr","source":"s","title":"Hi"}""",
            new EntryValidationOptions { Strict = true });

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "block.missingRequired");
        Assert.Empty(result.Warnings); // promoted, not duplicated
    }

    [Fact]
    public void Validate_TemplateStrict_PromotesWarningsWithoutStrictOption()
    {
        var validator = CreateValidator(PrTemplateWithRequiredSummary(strict: true));

        var result = validator.Validate("""{"type":"pr","source":"s","title":"Hi"}""");

        Assert.False(result.Ok);
        Assert.Contains(result.Errors, e => e.Code == "block.missingRequired");
    }

    [Fact]
    public void Validate_PresentRequiredBlock_StrictPasses()
    {
        var validator = CreateValidator(PrTemplateWithRequiredSummary());

        var result = validator.Validate(
            """{"type":"pr","source":"s","title":"Hi","content":[{"type":"markdown","label":"Summary","body":"ok"}]}""",
            new EntryValidationOptions { Strict = true });

        Assert.True(result.Ok);
    }

    [Fact]
    public void Validate_TagAliasesAndCaseFold_NormalizeAndDeduplicate()
    {
        var template = new EntryTemplate
        {
            Type = "pr",
            TagAliases = new Dictionary<string, string> { ["back-end"] = "backend" },
            TagCaseMode = TagCaseMode.Lower
        };
        var validator = CreateValidator(template);

        var result = validator.Validate(
            """{"type":"pr","source":"s","title":"Hi","tags":["Back-End","URGENT","urgent"]}""",
            new EntryValidationOptions { IncludeNormalized = true });

        Assert.NotNull(result.Normalized);
        Assert.Equal(new[] { "backend", "urgent" }, result.Normalized!.Tags);
    }

    [Fact]
    public void Validate_DisallowedTag_WarnsButDoesNotDrop()
    {
        var template = new EntryTemplate
        {
            Type = "pr",
            AllowedTags = ["backend", "frontend"]
        };
        var validator = CreateValidator(template);

        var result = validator.Validate(
            """{"type":"pr","source":"s","title":"Hi","tags":["backend","random"]}""",
            new EntryValidationOptions { IncludeNormalized = true });

        Assert.True(result.Ok); // non-strict
        Assert.Contains(result.Warnings, w => w.Code == "tag.notAllowed" && w.Message.Contains("random"));
        Assert.Contains("random", result.Normalized!.Tags); // never silently stripped
    }

    [Fact]
    public void Validate_IncludeNormalizedFalse_DoesNotEchoEntry()
    {
        var result = CreateValidator().Validate("""{"type":"t","source":"s","title":"Hi"}""");
        Assert.Null(result.Normalized);
    }

    [Fact]
    public void Validate_ExceedingMaxDiagnostics_Truncates()
    {
        var result = CreateValidator().Validate(
            """{"type":"t","source":"","title":"","severity":"nope"}""",
            new EntryValidationOptions { MaxDiagnostics = 1 });

        Assert.False(result.Ok);
        Assert.True(result.Errors.Count + result.Warnings.Count <= 1);
        Assert.NotNull(result.Truncated);
        Assert.True(result.Truncated > 0);
    }
}

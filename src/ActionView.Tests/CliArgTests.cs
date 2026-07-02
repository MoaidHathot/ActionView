using ActionView.Cli;

namespace ActionView.Tests;

/// <summary>
/// Guards the CLI shortcut-option hardening: an empty --group-id/--group-label is omitted
/// (never injected as ""), and a flag-looking value — the signature of a shell dropping an
/// empty "" argument so the parser swallows the next flag — is rejected instead of silently
/// corrupting the entry.
/// </summary>
public class CliArgTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassifyStringFlag_EmptyOrMissing_Omits(string? value)
    {
        var disposition = CliArg.ClassifyStringFlag(value, out var cleaned);

        Assert.Equal(StringFlagDisposition.Omit, disposition);
        Assert.Equal(string.Empty, cleaned);
    }

    [Theory]
    [InlineData("--wait")]
    [InlineData("--strict")]
    [InlineData("--config")]
    [InlineData("-f")]
    [InlineData("-j")]
    public void ClassifyStringFlag_FlagLikeValue_Rejects(string value)
    {
        var disposition = CliArg.ClassifyStringFlag(value, out _);

        Assert.Equal(StringFlagDisposition.Reject, disposition);
    }

    [Theory]
    [InlineData("ci-1847", "ci-1847")]
    [InlineData("  ci-1847  ", "ci-1847")]
    [InlineData("CI Run #1847", "CI Run #1847")]
    [InlineData("release/2026.07", "release/2026.07")]
    public void ClassifyStringFlag_RealValue_UsesTrimmed(string value, string expected)
    {
        var disposition = CliArg.ClassifyStringFlag(value, out var cleaned);

        Assert.Equal(StringFlagDisposition.Use, disposition);
        Assert.Equal(expected, cleaned);
    }
}

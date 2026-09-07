using ActionView.Core.Services;

namespace ActionView.Tests;

public class PathExpanderTests : IDisposable
{
    // Unique per instance so these tests never collide with each other, with
    // ConfigLoaderTests, or with real variables in the developer's environment.
    private readonly string _varName = $"ACTIONVIEW_TEST_VAR_{Guid.NewGuid():N}";

    public PathExpanderTests()
    {
        Environment.SetEnvironmentVariable(_varName, @"C:\expanded");
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_varName, null);

    [Fact]
    public void Expand_BareVariable_Substitutes()
    {
        var result = PathExpander.Expand($"${_varName}", "dataDirectory");

        Assert.Equal(@"C:\expanded", result);
    }

    [Fact]
    public void Expand_BareVariableWithTrailingPath_StopsAtSeparator()
    {
        var result = PathExpander.Expand($"${_varName}/actionview", "dataDirectory");

        Assert.Equal(@"C:\expanded/actionview", result);
    }

    [Fact]
    public void Expand_BracedVariable_AllowsAdjacentNameCharacters()
    {
        var result = PathExpander.Expand($"${{{_varName}}}data", "dataDirectory");

        Assert.Equal(@"C:\expandeddata", result);
    }

    [Fact]
    public void Expand_MultipleReferences_SubstitutesAll()
    {
        var result = PathExpander.Expand($"${_varName}/a/${{{_varName}}}/b", "dataDirectory");

        Assert.Equal(@"C:\expanded/a/C:\expanded/b", result);
    }

    [Fact]
    public void Expand_DoubleDollar_YieldsLiteralDollar()
    {
        var result = PathExpander.Expand($"$${_varName}/data", "dataDirectory");

        Assert.Equal($"${_varName}/data", result);
    }

    [Fact]
    public void Expand_DollarNotStartingAName_IsLeftLiteral()
    {
        // UNC admin shares must survive untouched.
        var result = PathExpander.Expand(@"\\server\C$\logs", "fileAccess.allowedRoots");

        Assert.Equal(@"\\server\C$\logs", result);
    }

    [Fact]
    public void Expand_NoDollar_ReturnsInputUnchanged()
    {
        var result = PathExpander.Expand(@"C:\plain\path", "dataDirectory");

        Assert.Equal(@"C:\plain\path", result);
    }

    [Fact]
    public void Expand_UnsetVariable_ThrowsNamingVariableAndSetting()
    {
        var missing = $"ACTIONVIEW_MISSING_{Guid.NewGuid():N}";

        var ex = Assert.Throws<InvalidOperationException>(
            () => PathExpander.Expand($"${missing}/data", "dataDirectory"));

        Assert.Contains(missing, ex.Message);
        Assert.Contains("dataDirectory", ex.Message);
    }

    [Fact]
    public void Expand_UnterminatedBrace_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathExpander.Expand("${ONEDRIVE/data", "dataDirectory"));

        Assert.Contains("unterminated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expand_EmptyBracedReference_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => PathExpander.Expand("${}/data", "dataDirectory"));
    }

    [Fact]
    public void Expand_LeadingTilde_ExpandsToUserProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(home, PathExpander.Expand("~", "dataDirectory"));
        Assert.Equal(Path.Combine(home, ".actionview"), PathExpander.Expand("~/.actionview", "dataDirectory"));
        Assert.Equal(Path.Combine(home, ".actionview"), PathExpander.Expand(@"~\.actionview", "dataDirectory"));
    }

    [Fact]
    public void Expand_TildeNotAtStart_IsLeftLiteral()
    {
        Assert.Equal(@"C:\a~b", PathExpander.Expand(@"C:\a~b", "dataDirectory"));
    }

    [Fact]
    public void Expand_TildeUserForm_IsLeftLiteral()
    {
        Assert.Equal("~someone/data", PathExpander.Expand("~someone/data", "dataDirectory"));
    }

    [Fact]
    public void ExpandOptional_NullAndEmpty_PassThrough()
    {
        Assert.Null(PathExpander.ExpandOptional(null, "templates.externalDirectory"));
        Assert.Equal(string.Empty, PathExpander.ExpandOptional(string.Empty, "templates.externalDirectory"));
    }
}

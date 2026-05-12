using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class ActionParameterValidatorTests
{
    private static List<ActionParameter> Decl(params ActionParameter[] parameters) => [.. parameters];

    [Fact]
    public void Validate_RequiredMissing_ReturnsError()
    {
        var declared = Decl(new ActionParameter
        {
            Name = "body", Label = "Body", Type = ActionParameterType.Multiline, Required = true
        });

        var (errors, normalized) = ActionParameterValidator.Validate(declared, supplied: null);

        Assert.NotEmpty(errors);
        Assert.Null(normalized);
        Assert.Contains(errors, e => e.Contains("body"));
    }

    [Fact]
    public void Validate_RequiredEmptyString_ReturnsError()
    {
        var declared = Decl(new ActionParameter
        {
            Name = "body", Label = "Body", Type = ActionParameterType.Text, Required = true
        });

        var (errors, _) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["body"] = "" });

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_OptionalMissing_FillsWithEmptyOrDefault()
    {
        var declared = Decl(
            new ActionParameter { Name = "x", Label = "X", Type = ActionParameterType.Text, Required = false },
            new ActionParameter { Name = "y", Label = "Y", Type = ActionParameterType.Text, Default = "fallback" });

        var (errors, normalized) = ActionParameterValidator.Validate(declared, supplied: null);

        Assert.Empty(errors);
        Assert.NotNull(normalized);
        Assert.Equal(string.Empty, normalized!["x"]);
        Assert.Equal("fallback", normalized["y"]);
    }

    [Fact]
    public void Validate_NumberType_RejectsNonNumeric()
    {
        var declared = Decl(new ActionParameter { Name = "n", Label = "N", Type = ActionParameterType.Number });
        var (errors, _) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["n"] = "abc" });
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_BooleanType_NormalizesCasing()
    {
        var declared = Decl(new ActionParameter { Name = "b", Label = "B", Type = ActionParameterType.Boolean });
        var (errors, normalized) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["b"] = "True" });

        Assert.Empty(errors);
        Assert.Equal("true", normalized!["b"]);
    }

    [Fact]
    public void Validate_SelectType_RejectsValueOutsideOptions()
    {
        var declared = Decl(new ActionParameter
        {
            Name = "sev", Label = "Severity", Type = ActionParameterType.Select,
            Options = ["nit", "blocker"]
        });

        var (errors, _) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["sev"] = "huge" });

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_SelectType_AcceptsValidOption()
    {
        var declared = Decl(new ActionParameter
        {
            Name = "sev", Label = "Severity", Type = ActionParameterType.Select,
            Options = ["nit", "blocker"]
        });

        var (errors, normalized) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["sev"] = "nit" });

        Assert.Empty(errors);
        Assert.Equal("nit", normalized!["sev"]);
    }

    [Fact]
    public void Validate_UnknownSuppliedKey_ReturnsError()
    {
        // Surfacing typos early prevents a "{{param.bdy}}" placeholder from silently failing to substitute.
        var declared = Decl(new ActionParameter { Name = "body", Label = "Body" });

        var (errors, _) = ActionParameterValidator.Validate(declared,
            new Dictionary<string, string> { ["bdy"] = "oops" });

        Assert.Contains(errors, e => e.Contains("bdy"));
    }

    [Fact]
    public void Validate_InvalidParameterName_ReturnsError()
    {
        var declared = Decl(new ActionParameter { Name = "bad name!", Label = "Bad" });
        var (errors, _) = ActionParameterValidator.Validate(declared, supplied: null);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_NoDeclaredParameters_AllowsEmpty()
    {
        var (errors, normalized) = ActionParameterValidator.Validate(declared: null, supplied: null);
        Assert.Empty(errors);
        Assert.NotNull(normalized);
        Assert.Empty(normalized!);
    }
}

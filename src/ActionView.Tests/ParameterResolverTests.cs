using System.Text.Json;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class ParameterResolverTests
{
    private static readonly ParameterResolver Resolver = new();

    [Fact]
    public void Resolve_SubstitutesNamespacedPlaceholder()
    {
        var result = Resolver.Resolve(
            "Hello {{param.name}}!",
            new Dictionary<string, string> { ["name"] = "world" });

        Assert.Equal("Hello world!", result);
    }

    [Fact]
    public void Resolve_LeavesUnknownNameInPlace()
    {
        var result = Resolver.Resolve(
            "Hello {{param.missing}}!",
            new Dictionary<string, string> { ["other"] = "x" });

        Assert.Equal("Hello {{param.missing}}!", result);
    }

    [Fact]
    public void Resolve_DoesNotTouchSecretSyntax()
    {
        // {{SECRET}} (no "param." prefix) is the SecretResolver's namespace; ParameterResolver must leave it alone.
        var result = Resolver.Resolve(
            "{{SECRET}} and {{param.x}}",
            new Dictionary<string, string> { ["x"] = "value", ["SECRET"] = "shouldNotMatch" });

        Assert.Equal("{{SECRET}} and value", result);
    }

    [Fact]
    public void Resolve_NullParameters_ReturnsInputUnchanged()
    {
        var result = Resolver.Resolve("{{param.x}}", null);
        Assert.Equal("{{param.x}}", result);
    }

    [Fact]
    public void Resolve_HandlesMultiplePlaceholders()
    {
        var result = Resolver.Resolve(
            "{{param.a}}-{{param.b}}-{{param.a}}",
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        Assert.Equal("1-2-1", result);
    }
}

public class JsonElementParameterizerTests
{
    private static readonly ParameterResolver Resolver = new();

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Parameterize_SubstitutesStringLeafInsideObject()
    {
        var element = Parse("{\"body\":\"{{param.text}}\",\"count\":3}");
        var result = JsonElementParameterizer.Parameterize(
            element, leaf => Resolver.Resolve(leaf, new Dictionary<string, string> { ["text"] = "hello" }));

        Assert.Equal("{\"body\":\"hello\",\"count\":3}", result);
    }

    [Fact]
    public void Parameterize_EscapesJsonSpecialCharacters()
    {
        // User input containing quotes/newlines must not break the JSON payload.
        var element = Parse("{\"body\":\"{{param.text}}\"}");
        var result = JsonElementParameterizer.Parameterize(
            element, leaf => Resolver.Resolve(leaf, new Dictionary<string, string> { ["text"] = "she said \"hi\"\nand left" }));

        // Re-parse to confirm validity and round-trip the value.
        using var doc = JsonDocument.Parse(result);
        Assert.Equal("she said \"hi\"\nand left", doc.RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public void Parameterize_WalksNestedArraysAndObjects()
    {
        var element = Parse("{\"items\":[{\"v\":\"{{param.a}}\"},{\"v\":\"{{param.b}}\"}]}");
        var result = JsonElementParameterizer.Parameterize(
            element, leaf => Resolver.Resolve(leaf, new Dictionary<string, string> { ["a"] = "x", ["b"] = "y" }));

        using var doc = JsonDocument.Parse(result);
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal("x", items[0].GetProperty("v").GetString());
        Assert.Equal("y", items[1].GetProperty("v").GetString());
    }

    [Fact]
    public void Parameterize_PreservesNonStringLeavesAndNulls()
    {
        var element = Parse("{\"n\":42,\"b\":true,\"z\":null,\"s\":\"{{param.x}}\"}");
        var result = JsonElementParameterizer.Parameterize(
            element, leaf => Resolver.Resolve(leaf, new Dictionary<string, string> { ["x"] = "ok" }));

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(42, doc.RootElement.GetProperty("n").GetInt32());
        Assert.True(doc.RootElement.GetProperty("b").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("z").ValueKind);
        Assert.Equal("ok", doc.RootElement.GetProperty("s").GetString());
    }

    [Fact]
    public void Parameterize_NullParameters_ReturnsRawTextUnchanged()
    {
        var element = Parse("{\"a\":\"{{param.x}}\"}");
        var result = JsonElementParameterizer.Parameterize(element, leaf => Resolver.Resolve(leaf, null));

        // No parameters resolve, so the placeholder is preserved after the leaf walk.
        Assert.Equal("{\"a\":\"{{param.x}}\"}", result);
    }
}

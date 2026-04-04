using ActionView.Core.Models;
using ActionView.Core.Services;

namespace ActionView.Tests;

public class SecretResolverTests
{
    [Fact]
    public void Resolve_ReplacesDirectSecretValue()
    {
        var config = new AppConfig
        {
            Secrets = new Dictionary<string, string>
            {
                ["API_KEY"] = "my-secret-key-123"
            }
        };
        var resolver = new SecretResolver(config);

        var result = resolver.Resolve("Bearer {{API_KEY}}");

        Assert.Equal("Bearer my-secret-key-123", result);
    }

    [Fact]
    public void Resolve_ReplacesEnvVarReference()
    {
        Environment.SetEnvironmentVariable("ACTIONVIEW_TEST_SECRET", "env-secret-value");
        try
        {
            var config = new AppConfig
            {
                Secrets = new Dictionary<string, string>
                {
                    ["MY_SECRET"] = "env:ACTIONVIEW_TEST_SECRET"
                }
            };
            var resolver = new SecretResolver(config);

            var result = resolver.Resolve("token={{MY_SECRET}}");

            Assert.Equal("token=env-secret-value", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONVIEW_TEST_SECRET", null);
        }
    }

    [Fact]
    public void Resolve_FallsBackToDirectEnvVar()
    {
        Environment.SetEnvironmentVariable("ACTIONVIEW_DIRECT_VAR", "direct-value");
        try
        {
            var config = new AppConfig();
            var resolver = new SecretResolver(config);

            var result = resolver.Resolve("{{ACTIONVIEW_DIRECT_VAR}}");

            Assert.Equal("direct-value", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONVIEW_DIRECT_VAR", null);
        }
    }

    [Fact]
    public void Resolve_LeavesUnresolvedPlaceholdersIntact()
    {
        var config = new AppConfig();
        var resolver = new SecretResolver(config);

        var result = resolver.Resolve("{{NONEXISTENT_VAR}}");

        Assert.Equal("{{NONEXISTENT_VAR}}", result);
    }

    [Fact]
    public void Resolve_HandlesMultiplePlaceholders()
    {
        var config = new AppConfig
        {
            Secrets = new Dictionary<string, string>
            {
                ["HOST"] = "example.com",
                ["PORT"] = "8080"
            }
        };
        var resolver = new SecretResolver(config);

        var result = resolver.Resolve("https://{{HOST}}:{{PORT}}/api");

        Assert.Equal("https://example.com:8080/api", result);
    }

    [Fact]
    public void Resolve_NoPlaceholders_ReturnsOriginal()
    {
        var config = new AppConfig();
        var resolver = new SecretResolver(config);

        var result = resolver.Resolve("no placeholders here");

        Assert.Equal("no placeholders here", result);
    }
}

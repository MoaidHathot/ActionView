using System.Text.RegularExpressions;
using ActionView.Core.Models;

namespace ActionView.Core.Services;

/// <summary>
/// Resolves {{VAR}} placeholders in action command strings.
/// Looks up values from the AppConfig secrets map and environment variables.
///
/// Config secrets format: "FRIENDLY_NAME": "env:ENV_VAR_NAME"
/// If a secret value starts with "env:", the remainder is used as an environment variable name.
/// Otherwise the value is used directly.
/// </summary>
public sealed partial class SecretResolver
{
    private readonly AppConfig _config;

    public SecretResolver(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Resolves all {{VAR}} placeholders in the input string.
    /// </summary>
    public string Resolve(string input)
    {
        return PlaceholderPattern().Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            return ResolveVariable(varName) ?? match.Value; // Leave unresolved if not found
        });
    }

    private string? ResolveVariable(string name)
    {
        // Check config secrets first. Read live from the config object so that a
        // hot-reload of actionview.json (see ConfigWatcher) takes effect without
        // rebuilding this singleton.
        if (_config.Secrets.TryGetValue(name, out var secretValue))
        {
            if (secretValue.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            {
                var envVarName = secretValue[4..];
                return Environment.GetEnvironmentVariable(envVarName);
            }
            return secretValue;
        }

        // Fall back to direct environment variable lookup
        return Environment.GetEnvironmentVariable(name);
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}

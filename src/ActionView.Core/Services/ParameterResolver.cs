using System.Text.RegularExpressions;

namespace ActionView.Core.Services;

/// <summary>
/// Substitutes <c>{{param.NAME}}</c> placeholders in command strings with values supplied
/// at action-execution time (e.g. an edited PR comment, a user-chosen severity).
///
/// Lives in its own namespace from <see cref="SecretResolver"/> so:
/// <list type="bullet">
///   <item>secret names and parameter names cannot collide silently;</item>
///   <item>secret resolution remains the trailing pass and is unaffected by user input.</item>
/// </list>
///
/// Always run this resolver BEFORE <see cref="SecretResolver"/>.
/// </summary>
public sealed partial class ParameterResolver
{
    /// <summary>
    /// Resolves all <c>{{param.NAME}}</c> placeholders. Unknown names are left in place;
    /// callers should validate required parameters separately via <see cref="ActionParameterValidator"/>.
    /// </summary>
    public string Resolve(string input, IReadOnlyDictionary<string, string>? parameters)
    {
        if (string.IsNullOrEmpty(input) || parameters is null || parameters.Count == 0)
            return input;

        return PlaceholderPattern().Replace(input, match =>
        {
            var name = match.Groups[1].Value;
            return parameters.TryGetValue(name, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{param\.(\w+)\}\}")]
    private static partial Regex PlaceholderPattern();
}

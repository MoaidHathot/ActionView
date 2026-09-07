using System.Text;

namespace ActionView.Core.Services;

/// <summary>
/// Expands environment-variable references and a leading <c>~</c> in the path
/// settings of <c>actionview.json</c> (<c>dataDirectory</c>,
/// <c>templates.externalDirectory</c>, <c>fileAccess.allowedRoots</c>).
///
/// Supported syntax:
/// <list type="bullet">
///   <item><description><c>$NAME</c> — bare reference. The name runs while the
///   characters are ASCII letters, digits, or <c>_</c>, so
///   <c>$OneDrive/actionview</c> resolves cleanly.</description></item>
///   <item><description><c>${NAME}</c> — braced reference, needed when the
///   variable abuts following name characters (<c>${OneDrive}data</c>).</description></item>
///   <item><description><c>$$</c> — escape for a literal <c>$</c>.</description></item>
///   <item><description>A leading <c>~</c> (alone, or followed by a separator)
///   expands to the user profile directory.</description></item>
/// </list>
///
/// A <c>$</c> that does not begin a valid name is left alone, so UNC admin
/// shares such as <c>\\server\C$\logs</c> survive untouched.
///
/// An unset (or empty) variable is a hard error rather than a silent empty
/// expansion. Config paths feed directory creation, so quietly resolving a typo
/// to a wrong-but-plausible path would create a junk tree and hide the mistake.
/// </summary>
public static class PathExpander
{
    /// <summary>
    /// Expands <paramref name="value"/>. <paramref name="settingName"/> is the
    /// config key (e.g. <c>"dataDirectory"</c>) and is used only to make errors
    /// point at the offending setting.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A referenced environment variable is unset or empty, a <c>${</c> is
    /// unterminated, or a reference names an empty variable.
    /// </exception>
    public static string Expand(string value, string settingName)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return ExpandHome(ExpandVariables(value, settingName));
    }

    /// <summary>
    /// <see cref="Expand"/> for settings that may legitimately be absent.
    /// Null and empty input pass through unchanged.
    /// </summary>
    public static string? ExpandOptional(string? value, string settingName) =>
        string.IsNullOrEmpty(value) ? value : Expand(value, settingName);

    private static string ExpandVariables(string value, string settingName)
    {
        if (!value.Contains('$'))
            return value;

        var builder = new StringBuilder(value.Length);
        var index = 0;

        while (index < value.Length)
        {
            var current = value[index];
            if (current != '$')
            {
                builder.Append(current);
                index++;
                continue;
            }

            // "$$" escapes a literal dollar sign.
            if (index + 1 < value.Length && value[index + 1] == '$')
            {
                builder.Append('$');
                index += 2;
                continue;
            }

            // "${NAME}" — braced form.
            if (index + 1 < value.Length && value[index + 1] == '{')
            {
                var close = value.IndexOf('}', index + 2);
                if (close < 0)
                {
                    throw new InvalidOperationException(
                        $"{settingName}: unterminated \"${{\" in \"{value}\". " +
                        "Close the reference with '}' or escape the dollar sign as \"$$\".");
                }

                builder.Append(Lookup(value[(index + 2)..close], value, settingName));
                index = close + 1;
                continue;
            }

            // "$NAME" — bare form. A '$' that starts no valid name stays literal.
            var start = index + 1;
            var end = start;
            while (end < value.Length && IsNameChar(value[end], isFirst: end == start))
                end++;

            if (end == start)
            {
                builder.Append('$');
                index++;
                continue;
            }

            builder.Append(Lookup(value[start..end], value, settingName));
            index = end;
        }

        return builder.ToString();
    }

    private static bool IsNameChar(char c, bool isFirst) =>
        c == '_' || char.IsAsciiLetter(c) || (!isFirst && char.IsAsciiDigit(c));

    private static string Lookup(string name, string value, string settingName)
    {
        if (name.Length == 0)
        {
            throw new InvalidOperationException(
                $"{settingName}: empty variable reference in \"{value}\". " +
                "Name the variable, or escape the dollar sign as \"$$\".");
        }

        var resolved = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(resolved))
        {
            throw new InvalidOperationException(
                $"{settingName}: environment variable \"{name}\" (referenced by \"{value}\") is not set. " +
                "Set the variable, use a literal path, or escape the dollar sign as \"$$\" " +
                "if it is meant to be part of the path.");
        }

        return resolved;
    }

    private static string ExpandHome(string value)
    {
        if (value.Length == 0 || value[0] != '~')
            return value;

        // Only "~", "~/rest", and "~\rest" expand. "~user" is left alone rather
        // than guessed at.
        if (value.Length > 1 && value[1] != '/' && value[1] != '\\')
            return value;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return value;

        var rest = value.Length > 2 ? value[2..] : string.Empty;
        return rest.Length == 0 ? home : Path.Combine(home, rest);
    }
}

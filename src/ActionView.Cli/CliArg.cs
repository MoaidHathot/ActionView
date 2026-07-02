namespace ActionView.Cli;

/// <summary>What to do with a string shortcut option value (e.g. --group-id / --group-label).</summary>
public enum StringFlagDisposition
{
    /// <summary>Use the (trimmed) value.</summary>
    Use,

    /// <summary>Value is absent/empty — omit the field entirely (never inject "").</summary>
    Omit,

    /// <summary>Value looks like a flag — almost certainly a shell-dropped empty argument. Reject.</summary>
    Reject
}

/// <summary>Defensive parsing helpers for CLI shortcut options.</summary>
public static class CliArg
{
    /// <summary>
    /// Classifies a string shortcut option value:
    /// <list type="bullet">
    /// <item>null / empty / whitespace → <see cref="StringFlagDisposition.Omit"/> (don't inject an empty field).</item>
    /// <item>leading '-' (e.g. "--wait") → <see cref="StringFlagDisposition.Reject"/>. This happens when a shell
    /// drops an empty "" argument and the parser swallows the following flag as the value
    /// (<c>--group-id "" --wait</c> collapses to <c>--group-id --wait</c>), which would otherwise silently
    /// corrupt the field and eat the flag.</item>
    /// <item>otherwise → <see cref="StringFlagDisposition.Use"/> with the trimmed value.</item>
    /// </list>
    /// </summary>
    public static StringFlagDisposition ClassifyStringFlag(string? value, out string cleaned)
    {
        cleaned = string.Empty;

        if (value is null)
            return StringFlagDisposition.Omit;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return StringFlagDisposition.Omit;

        if (trimmed[0] == '-')
            return StringFlagDisposition.Reject;

        cleaned = trimmed;
        return StringFlagDisposition.Use;
    }
}

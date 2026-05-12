namespace ActionView.Core.Models;

/// <summary>
/// Declares a runtime input that the user (or an MCP client) supplies when triggering an action.
/// Values are referenced inside <see cref="ActionCommand"/> string fields as <c>{{param.NAME}}</c>.
/// They are resolved before secret substitution so user input cannot collide with secret names.
/// </summary>
public sealed class ActionParameter
{
    /// <summary>
    /// Identifier used in command placeholders as <c>{{param.NAME}}</c>.
    /// Must match the regex <c>[A-Za-z_][A-Za-z0-9_]*</c>.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>Human-friendly label shown next to the input field.</summary>
    public required string Label { get; set; }

    /// <summary>Field type (text, multiline, select, number, boolean).</summary>
    public ActionParameterType Type { get; set; } = ActionParameterType.Text;

    /// <summary>
    /// Optional default/seed value (e.g. an AI's draft comment).
    /// Always serialized as a string; for numeric/boolean types the value is parsed.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>Allowed values when <see cref="Type"/> is <see cref="ActionParameterType.Select"/>.</summary>
    public List<string>? Options { get; set; }

    /// <summary>If true, the user must supply a non-empty value before the action can run.</summary>
    public bool Required { get; set; }

    /// <summary>Placeholder text shown inside the input.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Optional help text displayed beneath the input.</summary>
    public string? HelpText { get; set; }
}

public enum ActionParameterType
{
    /// <summary>Single-line text input.</summary>
    Text,

    /// <summary>Multi-line textarea — used for comment drafts and long messages.</summary>
    Multiline,

    /// <summary>Dropdown limited to <see cref="ActionParameter.Options"/>.</summary>
    Select,

    /// <summary>Numeric input. Submitted as a string but validated as a number.</summary>
    Number,

    /// <summary>Checkbox; substituted as <c>"true"</c> or <c>"false"</c>.</summary>
    Boolean
}

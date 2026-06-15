namespace ActionView.Core.Models;

/// <summary>How a set of tag filters combines when selecting entries.</summary>
public enum TagMatchMode
{
    /// <summary>Match entries carrying ANY of the requested tags (OR).</summary>
    Any,

    /// <summary>Match entries carrying ALL of the requested tags (AND).</summary>
    All,
}

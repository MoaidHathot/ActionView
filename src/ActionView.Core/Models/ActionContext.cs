namespace ActionView.Core.Models;

/// <summary>
/// Ambient data available when resolving an action command's
/// <c>{{content.*}}</c> / <c>{{entry.*}}</c> references. Supplied by the
/// endpoint that triggers the action (which has already loaded the entry and,
/// for a section action, the owning block).
/// </summary>
public sealed class ActionContext
{
    /// <summary>The entry the action belongs to.</summary>
    public Entry? Entry { get; init; }

    /// <summary>
    /// For a section/block action, the block that owns the action — the target
    /// of <c>{{content.self}}</c>. Null for entry-level actions.
    /// </summary>
    public ContentBlock? SelfBlock { get; init; }
}

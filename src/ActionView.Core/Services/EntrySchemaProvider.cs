using Json.Schema;

namespace ActionView.Core.Services;

/// <summary>
/// Single source of truth for the ActionView entry JSON schema.
///
/// The schema file lives in the repo-root <c>schemas/</c> directory and ships
/// embedded in <c>ActionView.Core</c>. The validator, the CLI <c>schema</c>
/// command, and the MCP <c>get_schema</c> tool all read this one copy, so the
/// published contract and the enforced contract can never drift apart.
/// </summary>
public static class EntrySchemaProvider
{
    private const string ResourceName = "entry.v1.schema.json";

    private static readonly Lazy<string> LazyRawJson = new(LoadRawJson);
    private static readonly Lazy<JsonSchema> LazySchema = new(() => JsonSchema.FromText(RawJson));

    /// <summary>The raw schema JSON text (for <c>get_schema</c> / the CLI <c>schema</c> command).</summary>
    public static string RawJson => LazyRawJson.Value;

    /// <summary>The parsed, cached schema ready for evaluation.</summary>
    public static JsonSchema Schema => LazySchema.Value;

    private static string LoadRawJson()
    {
        var assembly = typeof(EntrySchemaProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{ResourceName}' not found in assembly '{assembly.GetName().Name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

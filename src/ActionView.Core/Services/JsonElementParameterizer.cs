using System.Text.Json;
using System.Text.Json.Nodes;

namespace ActionView.Core.Services;

/// <summary>
/// Walks an <see cref="JsonElement"/> tree (typically an HTTP request body) and substitutes
/// <c>{{param.NAME}}</c> placeholders inside string leaves only.
///
/// Substituting at the leaf level (rather than serializing → regex → re-parsing) means that user
/// input containing JSON-special characters such as <c>"</c>, <c>\</c> or newlines cannot break
/// the resulting JSON: System.Text.Json escapes the value when the tree is serialized.
/// </summary>
public static class JsonElementParameterizer
{
    /// <summary>
    /// Returns the raw JSON for <paramref name="element"/> with each string leaf
    /// transformed by <paramref name="resolveLeaf"/> (typically parameter +
    /// content-reference substitution). Non-string leaves are emitted as-is.
    /// Because substitution happens at the leaf level, values containing JSON
    /// special characters cannot break the resulting JSON — System.Text.Json
    /// escapes them on serialization.
    /// </summary>
    public static string Parameterize(JsonElement element, Func<string, string> resolveLeaf)
    {
        var node = ConvertAndSubstitute(element, resolveLeaf);
        return node?.ToJsonString() ?? "null";
    }

    private static JsonNode? ConvertAndSubstitute(JsonElement element, Func<string, string> resolveLeaf)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var obj = new JsonObject();
                    foreach (var prop in element.EnumerateObject())
                        obj[prop.Name] = ConvertAndSubstitute(prop.Value, resolveLeaf);
                    return obj;
                }
            case JsonValueKind.Array:
                {
                    var arr = new JsonArray();
                    foreach (var item in element.EnumerateArray())
                        arr.Add(ConvertAndSubstitute(item, resolveLeaf));
                    return arr;
                }
            case JsonValueKind.String:
                {
                    var raw = element.GetString() ?? string.Empty;
                    return JsonValue.Create(resolveLeaf(raw));
                }
            case JsonValueKind.Number:
                // Preserve numeric precision by round-tripping through the raw text.
                return JsonNode.Parse(element.GetRawText());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }
}

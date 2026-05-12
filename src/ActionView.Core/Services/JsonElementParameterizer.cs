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
    /// Returns the raw JSON for <paramref name="element"/> with parameter placeholders substituted
    /// inside any string-valued leaves. Non-string leaves (numbers, booleans, null) are emitted as-is.
    /// </summary>
    public static string Parameterize(JsonElement element, ParameterResolver resolver, IReadOnlyDictionary<string, string>? parameters)
    {
        // Fast path: nothing to substitute.
        if (parameters is null || parameters.Count == 0)
            return element.GetRawText();

        var node = ConvertAndSubstitute(element, resolver, parameters);
        return node?.ToJsonString() ?? "null";
    }

    private static JsonNode? ConvertAndSubstitute(JsonElement element, ParameterResolver resolver, IReadOnlyDictionary<string, string> parameters)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var obj = new JsonObject();
                    foreach (var prop in element.EnumerateObject())
                        obj[prop.Name] = ConvertAndSubstitute(prop.Value, resolver, parameters);
                    return obj;
                }
            case JsonValueKind.Array:
                {
                    var arr = new JsonArray();
                    foreach (var item in element.EnumerateArray())
                        arr.Add(ConvertAndSubstitute(item, resolver, parameters));
                    return arr;
                }
            case JsonValueKind.String:
                {
                    var raw = element.GetString() ?? string.Empty;
                    return JsonValue.Create(resolver.Resolve(raw, parameters));
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

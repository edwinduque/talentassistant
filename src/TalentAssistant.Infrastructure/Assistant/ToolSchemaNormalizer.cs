using System.Text.Json.Nodes;

namespace TalentAssistant.Infrastructure.Assistant;

/// <summary>
/// Normalizes a tool JSON Schema to the subset accepted by stricter providers
/// (SPEC-022 S10). Gemini's function schema does not accept nullable or
/// multi-type unions, arrays without items or additionalProperties, so the
/// adapter keeps the first non-null type, adds a generic item schema and drops
/// the extra-properties flag. The canonical schema is never modified.
/// </summary>
public static class ToolSchemaNormalizer
{
    public static JsonNode? Normalize(JsonNode? schema)
    {
        switch (schema)
        {
            case JsonObject obj:
                NormalizeObject(obj);
                foreach (var property in obj)
                {
                    Normalize(property.Value);
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    Normalize(item);
                }

                break;
        }

        return schema;
    }

    private static void NormalizeObject(JsonObject obj)
    {
        // Not part of the Gemini function schema subset; the backend still
        // validates every argument.
        obj.Remove("additionalProperties");

        if (!obj.TryGetPropertyValue("type", out var typeNode)
            || typeNode is not JsonArray typeArray)
        {
            return;
        }

        var values = new List<string>();
        foreach (var item in typeArray)
        {
            if (item is JsonValue value
                && value.TryGetValue<string>(out var type)
                && !string.Equals(type, "null", StringComparison.OrdinalIgnoreCase)
                && !values.Contains(type, StringComparer.Ordinal))
            {
                values.Add(type);
            }
        }

        if (values.Count >= 1)
        {
            // Gemini also rejects the multi-type union used by the filters
            // option (["array","object","null"]). Keep the primary non-null
            // type: the description still tells the model to send an array.
            obj["type"] = values[0];
            if (string.Equals(values[0], "array", StringComparison.Ordinal)
                && !obj.ContainsKey("items"))
            {
                // Gemini requires items for ARRAY; the parent description
                // carries the filter shape.
                obj["items"] = new JsonObject { ["type"] = "object" };
            }
        }
    }
}

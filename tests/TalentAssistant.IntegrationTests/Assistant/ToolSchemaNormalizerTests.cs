using System.Text.Json.Nodes;
using TalentAssistant.Infrastructure.Assistant;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// SPEC-022 S10: the Gemini schema adapter removes nullable unions while the
/// canonical Groq schema stays untouched.
/// </summary>
public sealed class ToolSchemaNormalizerTests
{
    [Fact]
    public void NullableUnionTypesAreReducedToTheirNonNullType()
    {
        var schema = JsonNode.Parse(
            """
            {
              "type": "object",
              "properties": {
                "name": { "type": ["string", "null"] },
                "limit": { "type": ["integer", "null"] },
                "items": {
                  "type": ["array", "null"],
                  "items": { "type": ["string", "null"] }
                },
                "filters": { "type": ["array", "object", "null"] }
              }
            }
            """)!;

        ToolSchemaNormalizer.Normalize(schema);

        Assert.Equal("string", schema["properties"]!["name"]!["type"]!.GetValue<string>());
        Assert.Equal("integer", schema["properties"]!["limit"]!["type"]!.GetValue<string>());
        Assert.Equal("array", schema["properties"]!["items"]!["type"]!.GetValue<string>());
        Assert.Equal(
            "string",
            schema["properties"]!["items"]!["items"]!["type"]!.GetValue<string>());
        Assert.Equal("array", schema["properties"]!["filters"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void ArraysWithoutItemsGetAGenericObjectItem()
    {
        var schema = JsonNode.Parse(
            """
            {
              "type": "object",
              "properties": {
                "filters": { "type": ["array", "object", "null"] }
              },
              "additionalProperties": false
            }
            """)!;

        ToolSchemaNormalizer.Normalize(schema);

        Assert.Equal("array", schema["properties"]!["filters"]!["type"]!.GetValue<string>());
        Assert.Equal(
            "object",
            schema["properties"]!["filters"]!["items"]!["type"]!.GetValue<string>());
        Assert.False(schema.AsObject().ContainsKey("additionalProperties"));
    }

    [Fact]
    public void NonUnionTypesAndEnumsStayUnchanged()
    {
        var schema = JsonNode.Parse(
            """
            {
              "type": "object",
              "properties": {
                "dataset": { "type": "string", "enum": ["employees", "presence"] },
                "filters": { "type": "array", "items": { "type": "object" } }
              }
            }
            """)!;
        var before = schema.ToJsonString();

        ToolSchemaNormalizer.Normalize(schema);

        Assert.Equal(before, schema.ToJsonString());
    }

    [Fact]
    public void NormalizationIsIdempotent()
    {
        var schema = JsonNode.Parse("""{ "type": ["string", "null"] }""")!;

        ToolSchemaNormalizer.Normalize(schema);
        var once = schema.ToJsonString();
        ToolSchemaNormalizer.Normalize(schema);

        Assert.Equal(once, schema.ToJsonString());
    }
}

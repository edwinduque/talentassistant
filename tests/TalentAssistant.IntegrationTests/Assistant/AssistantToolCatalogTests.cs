using System.Text.Json;
using TalentAssistant.Application.Assistant;

namespace TalentAssistant.IntegrationTests.Assistant;

/// <summary>
/// The tool catalog is a fixed, closed registry (SPEC-009 §6, SPEC-008 §6):
/// these five names are the contract the model sees and the bench questions
/// depend on. Adding or renaming a tool must fail here first.
/// </summary>
public sealed class AssistantToolCatalogTests
{
    [Fact]
    public void CatalogExposesExactlyTheFiveFixedToolNames()
    {
        var names = ChatOrchestrator.ToolCatalog
            .Select(tool => tool.Name)
            .ToArray();

        Assert.Equal(
            new[] { "search_employees", "presence", "timeoff", "tasks", "attendance_insights" },
            names);
    }

    [Fact]
    public void EveryToolHasADescriptionAndAValidJsonObjectSchema()
    {
        Assert.All(ChatOrchestrator.ToolCatalog, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));

            using var schema = JsonDocument.Parse(tool.ParametersJson);
            Assert.Equal(JsonValueKind.Object, schema.RootElement.ValueKind);
            Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());
        });
    }

    [Fact]
    public void EveryParameterAcceptsNullBecauseTheProviderValidatesArgumentsAgainstTheSchema()
    {
        // The provider gateway hard-rejects (HTTP 400, tool_use_failed) any tool
        // call whose arguments violate this schema, and models legitimately
        // express "use the default" as null. Optional fields are therefore
        // declared ["string","null"] and normalized to "absent" by the
        // orchestrator; required range fields accept null too so the
        // orchestrator returns a tool error the model can correct instead of
        // the gateway killing the whole turn. Verified against the live
        // gateway on 2026-10-18 (presence {date:null} reproduced 400/200).
        Assert.All(ChatOrchestrator.ToolCatalog, tool =>
        {
            using var schema = JsonDocument.Parse(tool.ParametersJson);
            var properties = schema.RootElement.GetProperty("properties");

            Assert.All(properties.EnumerateObject(), property =>
            {
                var type = property.Value.GetProperty("type");
                Assert.True(
                    type.ValueKind == JsonValueKind.Array,
                    $"{tool.Name}.{property.Name} must allow null (array type), found '{type}'.");
                Assert.Contains(
                    "null",
                    type.EnumerateArray().Select(entry => entry.GetString()));
            });
        });
    }
}

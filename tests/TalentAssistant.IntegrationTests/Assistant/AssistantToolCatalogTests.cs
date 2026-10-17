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
}

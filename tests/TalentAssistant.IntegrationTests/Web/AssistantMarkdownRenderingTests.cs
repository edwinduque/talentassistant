extern alias Web;

using AssistantMarkdown = Web::TalentAssistant.Web.Components.Shared.AssistantMarkdown;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TalentAssistant.IntegrationTests.Web;

/// <summary>
/// SPEC-009 §15 S4: the answer is model output, so the renderer must encode it
/// as text instead of interpreting HTML. This renders the real component through
/// the static Blazor renderer and inspects the produced HTML.
/// </summary>
public sealed class AssistantMarkdownRenderingTests
{
    [Fact]
    public async Task ModelHtmlIsRenderedAsEncodedText()
    {
        const string answer = "<script>alert(1)</script>";
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Text"] = answer });
            var output = await renderer.RenderComponentAsync<AssistantMarkdown>(parameters);
            return output.ToHtmlString();
        });

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task MarkdownIsRenderedWithFormattingElements()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    ["Text"] = "**Total:** 3 personas\n\n- Uno\n- Dos"
                });
            var output = await renderer.RenderComponentAsync<AssistantMarkdown>(parameters);
            return output.ToHtmlString();
        });

        Assert.Contains("<strong>Total:</strong>", html);
        Assert.Contains("<ul", html);
        Assert.Contains("<li>Uno</li>", html);
    }
}

extern alias Web;

using AssistantBlock = Web::TalentAssistant.Web.Formatting.AssistantBlock;
using AssistantCodeSpan = Web::TalentAssistant.Web.Formatting.AssistantCodeSpan;
using AssistantEmphasisSpan = Web::TalentAssistant.Web.Formatting.AssistantEmphasisSpan;
using AssistantHeadingBlock = Web::TalentAssistant.Web.Formatting.AssistantHeadingBlock;
using AssistantLine = Web::TalentAssistant.Web.Formatting.AssistantLine;
using AssistantListBlock = Web::TalentAssistant.Web.Formatting.AssistantListBlock;
using AssistantMarkdownFormatter = Web::TalentAssistant.Web.Formatting.AssistantMarkdownFormatter;
using AssistantParagraphBlock = Web::TalentAssistant.Web.Formatting.AssistantParagraphBlock;
using AssistantSpan = Web::TalentAssistant.Web.Formatting.AssistantSpan;
using AssistantStrongSpan = Web::TalentAssistant.Web.Formatting.AssistantStrongSpan;
using AssistantTextSpan = Web::TalentAssistant.Web.Formatting.AssistantTextSpan;

namespace TalentAssistant.IntegrationTests.Web;

/// <summary>
/// SPEC-009 §15: the safe markdown subset rendered by the chat. The parser must
/// recognize the agreed syntax and leave everything else literal, because the
/// answer is model output and never trusted HTML.
/// </summary>
public sealed class AssistantMarkdownFormatterTests
{
    [Fact]
    public void ParagraphKeepsSimpleLineBreaks()
    {
        var blocks = AssistantMarkdownFormatter.Parse("Primera línea\nSegunda línea");

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        Assert.Equal(2, paragraph.Lines.Count);
        Assert.Equal("Primera línea", Text(paragraph.Lines[0]));
        Assert.Equal("Segunda línea", Text(paragraph.Lines[1]));
    }

    [Fact]
    public void BlankLineSeparatesParagraphs()
    {
        var blocks = AssistantMarkdownFormatter.Parse("Uno\n\ndos");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.IsType<AssistantParagraphBlock>(block));
    }

    [Fact]
    public void BulletListGroupsConsecutiveItems()
    {
        var blocks = AssistantMarkdownFormatter.Parse("- uno\n* dos\n+ tres");

        var list = Assert.IsType<AssistantListBlock>(Assert.Single(blocks));
        Assert.False(list.Ordered);
        Assert.Equal(["uno", "dos", "tres"], list.Items.Select(Text));
    }

    [Fact]
    public void OrderedListIsParsed()
    {
        var blocks = AssistantMarkdownFormatter.Parse("1. uno\n2) dos");

        var list = Assert.IsType<AssistantListBlock>(Assert.Single(blocks));
        Assert.True(list.Ordered);
        Assert.Equal(["uno", "dos"], list.Items.Select(Text));
    }

    [Fact]
    public void ParagraphFollowedByAListWithoutABlankLineSplitsBlocks()
    {
        var blocks = AssistantMarkdownFormatter.Parse("Datos:\n- uno\n- dos");

        Assert.Equal(2, blocks.Count);
        var paragraph = Assert.IsType<AssistantParagraphBlock>(blocks[0]);
        Assert.Equal("Datos:", Text(Assert.Single(paragraph.Lines)));
        var list = Assert.IsType<AssistantListBlock>(blocks[1]);
        Assert.Equal(2, list.Items.Count);
    }

    [Theory]
    [InlineData("# Uno", 1)]
    [InlineData("## Dos", 2)]
    [InlineData("### Tres", 3)]
    public void HeadingLevelsAreParsed(string input, int expectedLevel)
    {
        var blocks = AssistantMarkdownFormatter.Parse(input);

        var heading = Assert.IsType<AssistantHeadingBlock>(Assert.Single(blocks));
        Assert.Equal(expectedLevel, heading.Level);
    }

    [Fact]
    public void MoreThanThreeHashesStaysParagraph()
    {
        var blocks = AssistantMarkdownFormatter.Parse("#### No es encabezado");

        Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void BoldItalicAndInlineCodeAreParsed()
    {
        var blocks = AssistantMarkdownFormatter.Parse("**Total** y *cursiva* y `codigo`");

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        var spans = paragraph.Lines[0].Spans;

        Assert.Collection(
            spans,
            span => Assert.Equal("Total", Text(Assert.IsType<AssistantStrongSpan>(span))),
            span => Assert.Equal(" y ", Text(span)),
            span => Assert.Equal("cursiva", Text(Assert.IsType<AssistantEmphasisSpan>(span))),
            span => Assert.Equal(" y ", Text(span)),
            span => Assert.Equal("codigo", Assert.IsType<AssistantCodeSpan>(span).Text));
    }

    [Fact]
    public void NestedBoldInsideItalicKeepsWorking()
    {
        var blocks = AssistantMarkdownFormatter.Parse("*a **b** c*");

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        var emphasis = Assert.IsType<AssistantEmphasisSpan>(Assert.Single(paragraph.Lines[0].Spans));
        Assert.Contains(emphasis.Spans, span => span is AssistantStrongSpan);
        Assert.Equal("a b c", string.Concat(emphasis.Spans.Select(Text)));
    }

    [Theory]
    [InlineData("**sin cerrar")]
    [InlineData("`sin cerrar")]
    [InlineData("*sin cerrar")]
    public void UnbalancedMarkersStayLiteral(string input)
    {
        var blocks = AssistantMarkdownFormatter.Parse(input);

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        Assert.Equal(input, Text(Assert.Single(paragraph.Lines)));
    }

    [Fact]
    public void HtmlMarkupStaysText()
    {
        const string input = "<script>alert(1)</script>";
        var blocks = AssistantMarkdownFormatter.Parse(input);

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        var text = Assert.IsType<AssistantTextSpan>(Assert.Single(paragraph.Lines[0].Spans));
        Assert.Equal(input, text.Text);
    }

    [Fact]
    public void UnderscoreIdentifiersStayLiteral()
    {
        const string input = "user_name y __init__";
        var blocks = AssistantMarkdownFormatter.Parse(input);

        var paragraph = Assert.IsType<AssistantParagraphBlock>(Assert.Single(blocks));
        Assert.Equal(input, Text(Assert.Single(paragraph.Lines)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void EmptyAnswerProducesNoBlocks(string? input)
    {
        Assert.Empty(AssistantMarkdownFormatter.Parse(input));
    }

    private static string Text(AssistantLine line) =>
        string.Concat(line.Spans.Select(Text));

    private static string Text(AssistantSpan span) =>
        span switch
        {
            AssistantTextSpan text => text.Text,
            AssistantCodeSpan code => code.Text,
            AssistantStrongSpan strong => string.Concat(strong.Spans.Select(Text)),
            AssistantEmphasisSpan emphasis => string.Concat(emphasis.Spans.Select(Text)),
            _ => throw new InvalidOperationException($"Unexpected span type '{span.GetType()}'.")
        };
}

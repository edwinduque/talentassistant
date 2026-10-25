using System.Text;

namespace TalentAssistant.Web.Formatting;

/// <summary>
/// Presentation model of an assistant answer. The Web renders these records as
/// Blazor components, so the model text is always encoded and never interpreted
/// as HTML (SPEC-009 §15).
/// </summary>
public abstract record AssistantBlock;

public sealed record AssistantHeadingBlock(int Level, IReadOnlyList<AssistantSpan> Spans) : AssistantBlock;

public sealed record AssistantParagraphBlock(IReadOnlyList<AssistantLine> Lines) : AssistantBlock;

public sealed record AssistantListBlock(bool Ordered, IReadOnlyList<AssistantLine> Items) : AssistantBlock;

public sealed record AssistantLine(IReadOnlyList<AssistantSpan> Spans);

public abstract record AssistantSpan;

public sealed record AssistantTextSpan(string Text) : AssistantSpan;

public sealed record AssistantStrongSpan(IReadOnlyList<AssistantSpan> Spans) : AssistantSpan;

public sealed record AssistantEmphasisSpan(IReadOnlyList<AssistantSpan> Spans) : AssistantSpan;

public sealed record AssistantCodeSpan(string Text) : AssistantSpan;

/// <summary>
/// Parses the chat-friendly markdown subset approved in SPEC-009 §15: paragraphs,
/// simple line breaks, #/##/### headings, -/*/+ and numbered lists, **bold**,
/// *italic* and `inline code`. Unsupported or unbalanced syntax stays literal.
/// The parser produces text records only; it never builds HTML.
/// </summary>
public static class AssistantMarkdownFormatter
{
    private const int MaximumHeadingLevel = 3;

    public static IReadOnlyList<AssistantBlock> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<AssistantBlock>();
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var blocks = new List<AssistantBlock>();
        var index = 0;

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            if (TryParseHeading(lines[index], out var level, out var heading))
            {
                blocks.Add(new AssistantHeadingBlock(level, ParseSpans(heading)));
                index++;
                continue;
            }

            if (TryParseListItem(lines[index], out var ordered, out var item))
            {
                var items = new List<AssistantLine>();
                while (index < lines.Length
                    && TryParseListItem(lines[index], out var itemOrdered, out var nextItem)
                    && itemOrdered == ordered)
                {
                    items.Add(new AssistantLine(ParseSpans(nextItem)));
                    index++;
                }

                blocks.Add(new AssistantListBlock(ordered, items));
                continue;
            }

            // A paragraph keeps its own line breaks: chat answers usually list
            // one value per line even without a bullet marker, and collapsing
            // them would merge unrelated rows.
            var paragraph = new List<AssistantLine>();
            while (index < lines.Length
                && !string.IsNullOrWhiteSpace(lines[index])
                && !TryParseHeading(lines[index], out _, out _)
                && !TryParseListItem(lines[index], out _, out _))
            {
                paragraph.Add(new AssistantLine(ParseSpans(lines[index].Trim())));
                index++;
            }

            blocks.Add(new AssistantParagraphBlock(paragraph));
        }

        return blocks;
    }

    public static IReadOnlyList<AssistantSpan> ParseSpans(string text)
    {
        var spans = new List<AssistantSpan>();
        var buffer = new StringBuilder();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (current == '`')
            {
                var close = text.IndexOf('`', index + 1);
                if (close > index + 1)
                {
                    Flush();
                    spans.Add(new AssistantCodeSpan(text[(index + 1)..close]));
                    index = close + 1;
                    continue;
                }
            }
            else if (current == '*')
            {
                if (index + 1 < text.Length && text[index + 1] == '*')
                {
                    var close = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                    if (close > index + 2)
                    {
                        Flush();
                        spans.Add(new AssistantStrongSpan(ParseSpans(text[(index + 2)..close])));
                        index = close + 2;
                        continue;
                    }
                }
                else
                {
                    var close = FindSingleStar(text, index + 1);
                    if (close > index + 1)
                    {
                        Flush();
                        spans.Add(new AssistantEmphasisSpan(ParseSpans(text[(index + 1)..close])));
                        index = close + 1;
                        continue;
                    }
                }
            }

            buffer.Append(current);
            index++;
        }

        Flush();
        return spans;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            spans.Add(new AssistantTextSpan(buffer.ToString()));
            buffer.Clear();
        }
    }

    private static bool TryParseHeading(string line, out int level, out string content)
    {
        level = 0;
        content = string.Empty;
        var trimmed = line.TrimStart();
        var hashes = 0;
        while (hashes < trimmed.Length && trimmed[hashes] == '#')
        {
            hashes++;
        }

        if (hashes is < 1 or > MaximumHeadingLevel
            || hashes >= trimmed.Length
            || !char.IsWhiteSpace(trimmed[hashes]))
        {
            return false;
        }

        content = trimmed[(hashes + 1)..].Trim();
        if (content.Length == 0)
        {
            return false;
        }

        level = hashes;
        return true;
    }

    private static bool TryParseListItem(string line, out bool ordered, out string content)
    {
        ordered = false;
        content = string.Empty;
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] is '-' or '*' or '+')
        {
            if (trimmed.Length == 1 || !char.IsWhiteSpace(trimmed[1]))
            {
                return false;
            }

            content = trimmed[2..].Trim();
            return content.Length > 0;
        }

        var digits = 0;
        while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits]))
        {
            digits++;
        }

        if (digits == 0
            || digits + 1 >= trimmed.Length
            || trimmed[digits] is not ('.' or ')')
            || !char.IsWhiteSpace(trimmed[digits + 1]))
        {
            return false;
        }

        content = trimmed[(digits + 2)..].Trim();
        if (content.Length == 0)
        {
            return false;
        }

        ordered = true;
        return true;
    }

    // A closing single star must not belong to a ** delimiter, so nested bold
    // inside italic keeps working.
    private static int FindSingleStar(string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] != '*')
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '*')
            {
                index++;
                continue;
            }

            return index;
        }

        return -1;
    }
}

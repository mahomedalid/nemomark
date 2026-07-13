using Knowledge.Core.Abstractions;
using Knowledge.Core.Models;
using Markdig;
using Markdig.Syntax;

namespace Knowledge.Ingestion;

/// <summary>
/// Parses Markdown into logical sections using heading boundaries. Each top-level or
/// second-level heading starts a new section; content is grouped under the nearest heading.
/// </summary>
public sealed class MarkdownParser : IMarkdownParser
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public ParsedMarkdown Parse(string markdown, string? fallbackTitle = null)
    {
        markdown ??= string.Empty;
        var document = Markdown.Parse(markdown, _pipeline);

        var sections = new List<MarkdownSection>();
        string? currentHeading = null;
        var currentLevel = 0;
        var buffer = new List<Block>();

        void Flush()
        {
            if (buffer.Count == 0 && currentHeading is null)
            {
                return;
            }

            var md = RenderBlocks(markdown, buffer);
            var plain = MarkdownToPlainText(md);
            if (currentHeading is null && string.IsNullOrWhiteSpace(plain))
            {
                buffer.Clear();
                return;
            }

            sections.Add(new MarkdownSection(currentHeading, currentLevel, md, plain));
            buffer.Clear();
        }

        foreach (var block in document)
        {
            if (block is HeadingBlock heading && heading.Level <= 3)
            {
                Flush();
                currentHeading = GetHeadingText(markdown, heading);
                currentLevel = heading.Level;
            }
            else
            {
                buffer.Add(block);
            }
        }

        Flush();

        var title = sections.FirstOrDefault(s => s.Level == 1)?.Heading
                    ?? sections.FirstOrDefault(s => s.Heading is not null)?.Heading
                    ?? fallbackTitle
                    ?? "Untitled";

        return new ParsedMarkdown(title, sections);
    }

    private static string GetHeadingText(string source, HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return string.Empty;
        }

        var span = heading.Inline.Span;
        var text = source.Substring(span.Start, span.End - span.Start + 1);
        return MarkdownToPlainText(text).Trim();
    }

    private string RenderBlocks(string source, IReadOnlyList<Block> blocks)
    {
        if (blocks.Count == 0)
        {
            return string.Empty;
        }

        var start = blocks[0].Span.Start;
        var end = blocks[^1].Span.End;
        if (start < 0 || end < start || end >= source.Length)
        {
            return string.Empty;
        }

        return source.Substring(start, end - start + 1).Trim();
    }

    private static string MarkdownToPlainText(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        // Strip common Markdown markers to approximate plain text.
        var text = markdown;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[#*_`>]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}

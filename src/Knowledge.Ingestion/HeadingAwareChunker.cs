using Knowledge.Core.Abstractions;
using Knowledge.Core.Models;

namespace Knowledge.Ingestion;

/// <summary>
/// Splits parsed sections into retrieval-sized chunks. Prefers heading boundaries and keeps
/// paragraphs together, targeting ~300-800 tokens per chunk. Oversized sections are split on
/// paragraph boundaries; tiny adjacent sections are merged.
/// </summary>
public sealed class HeadingAwareChunker : IChunker
{
    // Rough heuristic: ~4 characters per token.
    private const int CharsPerToken = 4;
    private readonly int _targetTokens;
    private readonly int _maxTokens;
    private readonly int _minTokens;

    public HeadingAwareChunker(int targetTokens = 550, int maxTokens = 800, int minTokens = 120)
    {
        _targetTokens = targetTokens;
        _maxTokens = maxTokens;
        _minTokens = minTokens;
    }

    public IReadOnlyList<ChunkDraft> Chunk(ParsedMarkdown document)
    {
        var drafts = new List<ChunkDraft>();
        var order = 0;

        var pending = new List<MarkdownSection>();
        var pendingTokens = 0;

        void FlushPending()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var heading = pending[0].Heading;
            var md = string.Join("\n\n", pending.Select(SectionText));
            var plain = string.Join(" ", pending.Select(p => p.PlainText)).Trim();
            if (!string.IsNullOrWhiteSpace(plain))
            {
                drafts.Add(new ChunkDraft(heading, document.Title, order++, md.Trim(), plain));
            }

            pending.Clear();
            pendingTokens = 0;
        }

        foreach (var section in document.Sections)
        {
            var sectionTokens = EstimateTokens(section.PlainText) + EstimateTokens(section.Heading ?? string.Empty);

            if (sectionTokens > _maxTokens)
            {
                FlushPending();
                foreach (var part in SplitLargeSection(section))
                {
                    drafts.Add(new ChunkDraft(section.Heading, document.Title, order++, part.Markdown, part.PlainText));
                }

                continue;
            }

            if (pendingTokens + sectionTokens > _maxTokens)
            {
                FlushPending();
            }

            pending.Add(section);
            pendingTokens += sectionTokens;

            if (pendingTokens >= _targetTokens)
            {
                FlushPending();
            }
        }

        // Merge a trailing tiny chunk into the previous one when possible.
        if (pending.Count > 0)
        {
            if (pendingTokens < _minTokens && drafts.Count > 0)
            {
                var last = drafts[^1];
                var mergedMd = (last.Markdown + "\n\n" + string.Join("\n\n", pending.Select(SectionText))).Trim();
                var mergedPlain = (last.PlainText + " " + string.Join(" ", pending.Select(p => p.PlainText))).Trim();
                drafts[^1] = last with { Markdown = mergedMd, PlainText = mergedPlain };
                pending.Clear();
            }
            else
            {
                FlushPending();
            }
        }

        return drafts;
    }

    private static string SectionText(MarkdownSection section)
    {
        if (string.IsNullOrEmpty(section.Heading))
        {
            return section.Markdown;
        }

        var hashes = new string('#', Math.Clamp(section.Level, 1, 6));
        return string.IsNullOrWhiteSpace(section.Markdown)
            ? $"{hashes} {section.Heading}"
            : $"{hashes} {section.Heading}\n\n{section.Markdown}";
    }

    private IEnumerable<(string Markdown, string PlainText)> SplitLargeSection(MarkdownSection section)
    {
        var paragraphs = section.Markdown.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var buffer = new List<string>();
        var tokens = 0;
        var headingPrefix = string.IsNullOrEmpty(section.Heading)
            ? string.Empty
            : $"{new string('#', Math.Clamp(section.Level, 1, 6))} {section.Heading}\n\n";

        foreach (var paragraph in paragraphs)
        {
            var pTokens = EstimateTokens(paragraph);
            if (tokens + pTokens > _maxTokens && buffer.Count > 0)
            {
                var md = headingPrefix + string.Join("\n\n", buffer);
                yield return (md.Trim(), ToPlain(md));
                buffer.Clear();
                tokens = 0;
            }

            buffer.Add(paragraph);
            tokens += pTokens;
        }

        if (buffer.Count > 0)
        {
            var md = headingPrefix + string.Join("\n\n", buffer);
            yield return (md.Trim(), ToPlain(md));
        }
    }

    private static string ToPlain(string markdown)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(markdown, @"[#*_`>]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / CharsPerToken);
}

using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Parses raw Markdown text into a structured document with sections.</summary>
public interface IMarkdownParser
{
    ParsedMarkdown Parse(string markdown, string? fallbackTitle = null);
}

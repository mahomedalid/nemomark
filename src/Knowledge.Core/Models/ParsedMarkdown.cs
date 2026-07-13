namespace Knowledge.Core.Models;

/// <summary>Result of parsing a Markdown document into logical sections.</summary>
public record ParsedMarkdown(string Title, IReadOnlyList<MarkdownSection> Sections);

/// <summary>A logical section of a Markdown document, keyed by its heading.</summary>
public record MarkdownSection(string? Heading, int Level, string Markdown, string PlainText);

namespace Knowledge.Core.Models;

/// <summary>
/// A candidate chunk produced by the chunker before persistence and enrichment.
/// </summary>
public record ChunkDraft(
    string? Heading,
    string? Section,
    int ChunkOrder,
    string Markdown,
    string PlainText);

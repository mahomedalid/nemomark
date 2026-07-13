using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>
/// Splits a parsed document into retrieval-sized chunks, preferring heading boundaries
/// and keeping paragraphs together.
/// </summary>
public interface IChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ParsedMarkdown document);
}

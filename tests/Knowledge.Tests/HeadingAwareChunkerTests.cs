using Knowledge.Core.Models;
using Knowledge.Ingestion;

namespace Knowledge.Tests;

public class HeadingAwareChunkerTests
{
    [Fact]
    public void Chunk_AssignsSequentialOrder()
    {
        var chunker = new HeadingAwareChunker(targetTokens: 20, maxTokens: 40, minTokens: 5);
        var sections = new List<MarkdownSection>
        {
            new("Section A", 2, new string('a', 200), new string('a', 200)),
            new("Section B", 2, new string('b', 200), new string('b', 200)),
            new("Section C", 2, new string('c', 200), new string('c', 200))
        };
        var parsed = new ParsedMarkdown("Doc", sections);

        var chunks = chunker.Chunk(parsed);

        Assert.NotEmpty(chunks);
        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkOrder);
        }
    }

    [Fact]
    public void Chunk_SplitsOversizedSection()
    {
        var chunker = new HeadingAwareChunker(targetTokens: 50, maxTokens: 60, minTokens: 5);
        var big = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => new string((char)('a' + i), 100)));
        var sections = new List<MarkdownSection>
        {
            new("Big", 2, big, big)
        };
        var parsed = new ParsedMarkdown("Doc", sections);

        var chunks = chunker.Chunk(parsed);

        Assert.True(chunks.Count > 1);
    }

    [Fact]
    public void Chunk_EmptyDocumentProducesNoChunks()
    {
        var chunker = new HeadingAwareChunker();
        var parsed = new ParsedMarkdown("Doc", new List<MarkdownSection>());

        var chunks = chunker.Chunk(parsed);

        Assert.Empty(chunks);
    }
}

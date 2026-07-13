using Pgvector;

namespace Knowledge.Core.Domain;

/// <summary>
/// A retrievable unit of knowledge produced by chunking a document.
/// </summary>
public class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    public KnowledgeDocument? Document { get; set; }

    public string? Heading { get; set; }

    public string? Section { get; set; }

    public int ChunkOrder { get; set; }

    public string Markdown { get; set; } = string.Empty;

    public string PlainText { get; set; } = string.Empty;

    /// <summary>AI-generated short summary of the chunk.</summary>
    public string? Summary { get; set; }

    /// <summary>Embedding vector. Null until embeddings are generated.</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Content hash of the chunk, used to detect changes and skip re-processing.</summary>
    public string Hash { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<KnowledgeTag> Tags { get; set; } = new();

    public List<KnowledgeEntity> Entities { get; set; } = new();
}

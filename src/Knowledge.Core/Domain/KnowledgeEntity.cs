namespace Knowledge.Core.Domain;

public class KnowledgeEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChunkId { get; set; }

    public KnowledgeChunk? Chunk { get; set; }

    public string Entity { get; set; } = string.Empty;
}

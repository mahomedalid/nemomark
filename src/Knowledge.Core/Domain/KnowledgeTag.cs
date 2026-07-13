namespace Knowledge.Core.Domain;

public class KnowledgeTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChunkId { get; set; }

    public KnowledgeChunk? Chunk { get; set; }

    public string Tag { get; set; } = string.Empty;
}

namespace Knowledge.Core.Domain;

/// <summary>
/// Represents a source Markdown document that has been ingested into the knowledge base.
/// </summary>
public class KnowledgeDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>Relative or absolute path of the source Markdown file.</summary>
    public string SourcePath { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Content hash of the full document, used to detect changes.</summary>
    public string Hash { get; set; } = string.Empty;

    public List<KnowledgeChunk> Chunks { get; set; } = new();
}

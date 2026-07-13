using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Generates structured metadata (summary, tags, keywords, entities) for a chunk.</summary>
public interface IMetadataEnricher
{
    Task<ChunkMetadata> EnrichAsync(string chunkText, CancellationToken cancellationToken = default);
}

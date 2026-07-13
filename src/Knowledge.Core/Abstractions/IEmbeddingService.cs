using Pgvector;

namespace Knowledge.Core.Abstractions;

/// <summary>Generates embedding vectors for text using an embedding model.</summary>
public interface IEmbeddingService
{
    Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vector>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}

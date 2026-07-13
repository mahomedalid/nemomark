using Knowledge.Core.Abstractions;
using Microsoft.Extensions.AI;
using Pgvector;

namespace Knowledge.Infrastructure.AI;

/// <summary>Generates embeddings via an Azure OpenAI embedding deployment.</summary>
public sealed class AzureOpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public AzureOpenAiEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator) =>
        _generator = generator;

    public async Task<Vector> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await _generator.GenerateAsync(new[] { text ?? string.Empty }, cancellationToken: cancellationToken);
        return new Vector(result[0].Vector);
    }

    public async Task<IReadOnlyList<Vector>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<Vector>();
        }

        var result = await _generator.GenerateAsync(texts, cancellationToken: cancellationToken);
        return result.Select(e => new Vector(e.Vector)).ToList();
    }
}

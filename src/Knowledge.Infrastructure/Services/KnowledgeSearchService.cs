using Knowledge.Core.Abstractions;
using Knowledge.Core.Models;
using Knowledge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Services;

/// <summary>Vector-similarity knowledge search.</summary>
public sealed class KnowledgeSearchService : IKnowledgeSearchService
{
    private readonly IEmbeddingService _embeddings;
    private readonly IKnowledgeRepository _repository;
    private readonly KnowledgeOptions _options;

    public KnowledgeSearchService(
        IEmbeddingService embeddings,
        IKnowledgeRepository repository,
        IOptions<KnowledgeOptions> options)
    {
        _embeddings = embeddings;
        _repository = repository;
        _options = options.Value;
    }

    public async Task<SearchResult> SearchAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return new SearchResult(question, Array.Empty<SearchHit>());
        }

        var embedding = await _embeddings.EmbedAsync(question, cancellationToken);
        var hits = await _repository.SearchByEmbeddingAsync(embedding, _options.TopK, cancellationToken);
        return new SearchResult(question, hits);
    }
}

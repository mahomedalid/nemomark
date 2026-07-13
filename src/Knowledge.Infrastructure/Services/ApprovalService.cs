using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Microsoft.Extensions.Logging;

namespace Knowledge.Infrastructure.Services;

/// <summary>
/// Promotes approved candidate knowledge into official knowledge chunks under a synthetic
/// "learned knowledge" document. Rejected candidates are archived (status only).
/// </summary>
public sealed class ApprovalService : IApprovalService
{
    private const string LearnedSourcePath = "learned://conversations";
    private const string LearnedTitle = "Learned Knowledge";

    private readonly IKnowledgeRepository _repository;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IKnowledgeRepository repository,
        IEmbeddingService embeddings,
        ILogger<ApprovalService> logger)
    {
        _repository = repository;
        _embeddings = embeddings;
        _logger = logger;
    }

    public Task<IReadOnlyList<CandidateKnowledge>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        _repository.GetCandidatesByStatusAsync(CandidateStatus.Pending, cancellationToken);

    public async Task<bool> ApproveAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _repository.GetCandidateAsync(candidateId, cancellationToken);
        if (candidate is null || candidate.Status != CandidateStatus.Pending)
        {
            return false;
        }

        var document = await _repository.GetDocumentByPathAsync(LearnedSourcePath, cancellationToken)
                       ?? new KnowledgeDocument
                       {
                           SourcePath = LearnedSourcePath,
                           Title = LearnedTitle,
                           Hash = string.Empty
                       };

        var chunk = new KnowledgeChunk
        {
            DocumentId = document.Id,
            Heading = "Learned fact",
            Section = LearnedTitle,
            ChunkOrder = document.Chunks.Count,
            Markdown = candidate.ExtractedFact,
            PlainText = candidate.ExtractedFact,
            Summary = candidate.ExtractedFact,
            Hash = ContentHasher.Hash(candidate.ExtractedFact),
            Embedding = await _embeddings.EmbedAsync(candidate.ExtractedFact, cancellationToken)
        };

        document.Chunks.Add(chunk);
        document.UpdatedUtc = DateTime.UtcNow;
        await _repository.UpsertDocumentAsync(document, cancellationToken);

        candidate.Status = CandidateStatus.Approved;
        await _repository.UpdateCandidateAsync(candidate, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Approved candidate {Id} into knowledge base.", candidateId);
        return true;
    }

    public async Task<bool> RejectAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _repository.GetCandidateAsync(candidateId, cancellationToken);
        if (candidate is null || candidate.Status != CandidateStatus.Pending)
        {
            return false;
        }

        candidate.Status = CandidateStatus.Rejected;
        await _repository.UpdateCandidateAsync(candidate, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

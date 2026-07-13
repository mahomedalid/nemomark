using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Pgvector;

namespace Knowledge.Core.Abstractions;

/// <summary>Persistence abstraction for the knowledge base.</summary>
public interface IKnowledgeRepository
{
    // Documents
    Task<KnowledgeDocument?> GetDocumentByPathAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task UpsertDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task DeleteDocumentByPathAsync(string sourcePath, CancellationToken cancellationToken = default);

    // Chunks
    Task<KnowledgeChunk?> GetChunkByHashAsync(Guid documentId, string hash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchHit>> SearchByEmbeddingAsync(Vector embedding, int topK, CancellationToken cancellationToken = default);
    Task AddChunkAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default);

    // Conversations
    Task<Conversation> GetOrCreateConversationAsync(Guid? conversationId, string? userId, CancellationToken cancellationToken = default);
    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default);

    // Candidate knowledge
    Task AddCandidateAsync(CandidateKnowledge candidate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateKnowledge>> GetCandidatesByStatusAsync(CandidateStatus status, CancellationToken cancellationToken = default);
    Task<CandidateKnowledge?> GetCandidateAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateCandidateAsync(CandidateKnowledge candidate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

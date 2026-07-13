using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeRepository : IKnowledgeRepository
{
    private readonly KnowledgeDbContext _db;

    public KnowledgeRepository(KnowledgeDbContext db) => _db = db;

    public Task<KnowledgeDocument?> GetDocumentByPathAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        _db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.SourcePath == sourcePath, cancellationToken);

    public async Task UpsertDocumentAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Documents
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == document.Id, cancellationToken);

        if (existing is null)
        {
            await _db.Documents.AddAsync(document, cancellationToken);
            return;
        }

        _db.Entry(existing).CurrentValues.SetValues(document);

        // Remove chunks that are no longer present.
        var keepIds = document.Chunks.Select(c => c.Id).ToHashSet();
        var toRemove = existing.Chunks.Where(c => !keepIds.Contains(c.Id)).ToList();
        _db.Chunks.RemoveRange(toRemove);

        foreach (var chunk in document.Chunks)
        {
            var tracked = existing.Chunks.FirstOrDefault(c => c.Id == chunk.Id);
            if (tracked is null)
            {
                chunk.DocumentId = existing.Id;
                await _db.Chunks.AddAsync(chunk, cancellationToken);
            }
            else if (!ReferenceEquals(tracked, chunk))
            {
                _db.Entry(tracked).CurrentValues.SetValues(chunk);
            }
        }
    }

    public async Task DeleteDocumentByPathAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.SourcePath == sourcePath, cancellationToken);
        if (doc is not null)
        {
            _db.Documents.Remove(doc);
        }
    }

    public Task<KnowledgeChunk?> GetChunkByHashAsync(Guid documentId, string hash, CancellationToken cancellationToken = default) =>
        _db.Chunks
            .Include(c => c.Tags)
            .Include(c => c.Entities)
            .FirstOrDefaultAsync(c => c.DocumentId == documentId && c.Hash == hash, cancellationToken);

    public async Task<IReadOnlyList<SearchHit>> SearchByEmbeddingAsync(Vector embedding, int topK, CancellationToken cancellationToken = default)
    {
        var results = await _db.Chunks
            .Where(c => c.Embedding != null)
            .Include(c => c.Document)
            .Include(c => c.Tags)
            .OrderBy(c => c.Embedding!.CosineDistance(embedding))
            .Take(topK)
            .Select(c => new { Chunk = c, Distance = c.Embedding!.CosineDistance(embedding) })
            .ToListAsync(cancellationToken);

        // Convert cosine distance (0..2) into a similarity score (1..-1).
        return results
            .Select(r => new SearchHit(r.Chunk, 1.0 - r.Distance))
            .ToList();
    }

    public async Task AddChunkAsync(KnowledgeChunk chunk, CancellationToken cancellationToken = default) =>
        await _db.Chunks.AddAsync(chunk, cancellationToken);

    public async Task<Conversation> GetOrCreateConversationAsync(Guid? conversationId, string? userId, CancellationToken cancellationToken = default)
    {
        if (conversationId is { } id)
        {
            var existing = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var conversation = new Conversation
        {
            Id = conversationId ?? Guid.NewGuid(),
            UserId = userId,
            StartedUtc = DateTime.UtcNow
        };
        await _db.Conversations.AddAsync(conversation, cancellationToken);
        return conversation;
    }

    public async Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default) =>
        await _db.Messages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default)
    {
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public async Task AddCandidateAsync(CandidateKnowledge candidate, CancellationToken cancellationToken = default) =>
        await _db.Candidates.AddAsync(candidate, cancellationToken);

    public async Task<IReadOnlyList<CandidateKnowledge>> GetCandidatesByStatusAsync(CandidateStatus status, CancellationToken cancellationToken = default) =>
        await _db.Candidates.Where(c => c.Status == status).OrderBy(c => c.CreatedUtc).ToListAsync(cancellationToken);

    public Task<CandidateKnowledge?> GetCandidateAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Candidates.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task UpdateCandidateAsync(CandidateKnowledge candidate, CancellationToken cancellationToken = default)
    {
        _db.Candidates.Update(candidate);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}

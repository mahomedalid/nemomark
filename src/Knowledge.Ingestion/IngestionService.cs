using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Microsoft.Extensions.Logging;

namespace Knowledge.Ingestion;

/// <summary>
/// Orchestrates the full ingestion pipeline for Markdown documents:
/// parse → chunk → enrich → embed → persist. Only changed chunks are reprocessed.
/// </summary>
public sealed class IngestionService : IIngestionService
{
    private static readonly string[] MarkdownExtensions = { ".md", ".markdown" };

    private readonly IMarkdownParser _parser;
    private readonly IChunker _chunker;
    private readonly IMetadataEnricher _enricher;
    private readonly IEmbeddingService _embeddings;
    private readonly IKnowledgeRepository _repository;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IMarkdownParser parser,
        IChunker chunker,
        IMetadataEnricher enricher,
        IEmbeddingService embeddings,
        IKnowledgeRepository repository,
        ILogger<IngestionService> logger)
    {
        _parser = parser;
        _chunker = chunker;
        _enricher = enricher;
        _embeddings = embeddings;
        _repository = repository;
        _logger = logger;
    }

    public async Task IngestDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Ingestion directory {Directory} does not exist.", directory);
            return;
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => MarkdownExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            await IngestFileAsync(file, markdown, cancellationToken);
        }
    }

    public async Task IngestFileAsync(string sourcePath, string markdown, CancellationToken cancellationToken = default)
    {
        var documentHash = ContentHasher.Hash(markdown);
        var existing = await _repository.GetDocumentByPathAsync(sourcePath, cancellationToken);

        if (existing is not null && existing.Hash == documentHash)
        {
            _logger.LogInformation("Document {Path} unchanged; skipping.", sourcePath);
            return;
        }

        var parsed = _parser.Parse(markdown, Path.GetFileNameWithoutExtension(sourcePath));
        var drafts = _chunker.Chunk(parsed);

        var document = existing ?? new KnowledgeDocument
        {
            SourcePath = sourcePath,
            CreatedUtc = DateTime.UtcNow
        };

        document.Title = parsed.Title;
        document.Hash = documentHash;
        document.UpdatedUtc = DateTime.UtcNow;
        if (existing is not null)
        {
            document.Version += 1;
        }

        // Replace chunks for the document. Reuse unchanged chunks (matched by hash) to avoid
        // regenerating embeddings and metadata.
        var newChunks = new List<KnowledgeChunk>();
        foreach (var draft in drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkHash = ContentHasher.Hash(draft.Markdown);
            var reusable = existing is null
                ? null
                : await _repository.GetChunkByHashAsync(document.Id, chunkHash, cancellationToken);

            if (reusable is not null)
            {
                reusable.ChunkOrder = draft.ChunkOrder;
                reusable.Heading = draft.Heading;
                reusable.Section = draft.Section;
                reusable.UpdatedUtc = DateTime.UtcNow;
                newChunks.Add(reusable);
                continue;
            }

            var chunk = new KnowledgeChunk
            {
                DocumentId = document.Id,
                Heading = draft.Heading,
                Section = draft.Section,
                ChunkOrder = draft.ChunkOrder,
                Markdown = draft.Markdown,
                PlainText = draft.PlainText,
                Hash = chunkHash,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            var metadata = await _enricher.EnrichAsync(draft.Markdown, cancellationToken);
            chunk.Summary = metadata.Summary;
            chunk.Tags = metadata.Tags.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(t => new KnowledgeTag { ChunkId = chunk.Id, Tag = t }).ToList();
            chunk.Entities = metadata.Entities.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(e => new KnowledgeEntity { ChunkId = chunk.Id, Entity = e }).ToList();

            var embeddingInput = BuildEmbeddingInput(draft.Heading, metadata.Summary, draft.PlainText);
            chunk.Embedding = await _embeddings.EmbedAsync(embeddingInput, cancellationToken);

            newChunks.Add(chunk);
        }

        document.Chunks = newChunks;
        await _repository.UpsertDocumentAsync(document, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ingested {Path}: {ChunkCount} chunks.", sourcePath, newChunks.Count);
    }

    public async Task RemoveFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteDocumentByPathAsync(sourcePath, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Removed document {Path}.", sourcePath);
    }

    private static string BuildEmbeddingInput(string? heading, string? summary, string plainText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(heading))
        {
            parts.Add(heading!);
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            parts.Add(summary!);
        }

        parts.Add(plainText);
        return string.Join("\n", parts);
    }
}

using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Knowledge.Infrastructure.AI;

/// <summary>Generates structured metadata for a chunk using the chat model.</summary>
public sealed class LlmMetadataEnricher : IMetadataEnricher
{
    private const string SystemPrompt =
        "You extract structured metadata from documentation chunks. " +
        "Respond ONLY with minified JSON matching this schema: " +
        "{\"summary\": string, \"tags\": string[], \"keywords\": string[], \"entities\": string[]}. " +
        "The summary must be one or two sentences. Tags are broad topics, keywords are salient terms, " +
        "entities are proper nouns (products, standards, systems). Do not include commentary.";

    private readonly ILlmChatClient _chat;
    private readonly ILogger<LlmMetadataEnricher> _logger;

    public LlmMetadataEnricher(ILlmChatClient chat, ILogger<LlmMetadataEnricher> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<ChunkMetadata> EnrichAsync(string chunkText, CancellationToken cancellationToken = default)
    {
        var messages = new (MessageRole, string)[]
        {
            (MessageRole.System, SystemPrompt),
            (MessageRole.User, chunkText)
        };

        try
        {
            var response = await _chat.CompleteAsync(messages, cancellationToken);
            var metadata = LlmJson.Deserialize<ChunkMetadata>(response);
            return metadata ?? new ChunkMetadata();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Metadata enrichment failed; returning empty metadata.");
            return new ChunkMetadata();
        }
    }
}

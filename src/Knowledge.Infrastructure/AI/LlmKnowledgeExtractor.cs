using System.Text.Json.Serialization;
using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Knowledge.Infrastructure.AI;

/// <summary>Extracts candidate factual statements from a conversation message.</summary>
public sealed class LlmKnowledgeExtractor : IKnowledgeExtractor
{
    private const string SystemPrompt =
        "You decide whether a message contains durable, factual information worth storing in a " +
        "knowledge base. Ignore opinions, questions, chit-chat, and transient context. " +
        "Respond ONLY with minified JSON: {\"facts\": [{\"text\": string, \"reason\": string, \"confidence\": number}]}. " +
        "If there is nothing worth storing, return {\"facts\": []}. Confidence is between 0 and 1.";

    private readonly ILlmChatClient _chat;
    private readonly ILogger<LlmKnowledgeExtractor> _logger;

    public LlmKnowledgeExtractor(ILlmChatClient chat, ILogger<LlmKnowledgeExtractor> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(string message, CancellationToken cancellationToken = default)
    {
        var messages = new (MessageRole, string)[]
        {
            (MessageRole.System, SystemPrompt),
            (MessageRole.User, message)
        };

        try
        {
            var response = await _chat.CompleteAsync(messages, cancellationToken);
            var dto = LlmJson.Deserialize<ExtractionDto>(response);
            if (dto?.Facts is null || dto.Facts.Count == 0)
            {
                return ExtractionResult.Empty;
            }

            var facts = dto.Facts
                .Where(f => !string.IsNullOrWhiteSpace(f.Text))
                .Select(f => new ExtractedFact(f.Text.Trim(), f.Reason?.Trim() ?? string.Empty, Math.Clamp(f.Confidence, 0, 1)))
                .ToList();

            return facts.Count == 0 ? ExtractionResult.Empty : new ExtractionResult(facts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Knowledge extraction failed; returning no facts.");
            return ExtractionResult.Empty;
        }
    }

    private sealed class ExtractionDto
    {
        [JsonPropertyName("facts")]
        public List<FactDto> Facts { get; set; } = new();
    }

    private sealed class FactDto
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}

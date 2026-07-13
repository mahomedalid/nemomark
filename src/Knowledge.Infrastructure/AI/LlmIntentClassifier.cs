using System.Text.Json.Serialization;
using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Microsoft.Extensions.Logging;

namespace Knowledge.Infrastructure.AI;

/// <summary>Classifies user message intent using the chat model.</summary>
public sealed class LlmIntentClassifier : IIntentClassifier
{
    private const string SystemPrompt =
        "Classify the user's message into exactly one intent. " +
        "Respond ONLY with minified JSON: {\"intent\": string, \"confidence\": number}. " +
        "Allowed intent values: Question, Conversation, Feedback, NewKnowledge, Correction. " +
        "Confidence is between 0 and 1.";

    private readonly ILlmChatClient _chat;
    private readonly ILogger<LlmIntentClassifier> _logger;

    public LlmIntentClassifier(ILlmChatClient chat, ILogger<LlmIntentClassifier> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task<IntentResult> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        var messages = new (MessageRole, string)[]
        {
            (MessageRole.System, SystemPrompt),
            (MessageRole.User, message)
        };

        try
        {
            var response = await _chat.CompleteAsync(messages, cancellationToken);
            var dto = LlmJson.Deserialize<IntentDto>(response);
            if (dto is null || !Enum.TryParse<IntentType>(dto.Intent, ignoreCase: true, out var intent))
            {
                return new IntentResult(IntentType.Question, 0.5);
            }

            return new IntentResult(intent, Math.Clamp(dto.Confidence, 0, 1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed; defaulting to Question.");
            return new IntentResult(IntentType.Question, 0.5);
        }
    }

    private sealed class IntentDto
    {
        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "Question";

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 0.5;
    }
}

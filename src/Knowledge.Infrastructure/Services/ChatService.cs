using System.Runtime.CompilerServices;
using System.Text;
using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Knowledge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Services;

/// <summary>
/// Top-level chat orchestration: intent detection → retrieval → context → LLM response,
/// followed by asynchronous knowledge extraction into candidate knowledge.
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IKnowledgeRepository _repository;
    private readonly IIntentClassifier _intentClassifier;
    private readonly IKnowledgeSearchService _search;
    private readonly IContextBuilder _contextBuilder;
    private readonly ILlmChatClient _chat;
    private readonly IKnowledgeExtractor _extractor;
    private readonly KnowledgeOptions _options;

    public ChatService(
        IKnowledgeRepository repository,
        IIntentClassifier intentClassifier,
        IKnowledgeSearchService search,
        IContextBuilder contextBuilder,
        ILlmChatClient chat,
        IKnowledgeExtractor extractor,
        IOptions<KnowledgeOptions> options)
    {
        _repository = repository;
        _intentClassifier = intentClassifier;
        _search = search;
        _contextBuilder = contextBuilder;
        _chat = chat;
        _extractor = extractor;
        _options = options.Value;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await _repository.GetOrCreateConversationAsync(request.ConversationId, request.UserId, cancellationToken);

        var intent = await _intentClassifier.ClassifyAsync(request.Message, cancellationToken);
        await PersistUserMessageAsync(conversation, request.Message, cancellationToken);

        var searchResult = await _search.SearchAsync(request.Message, cancellationToken);
        var history = await _repository.GetRecentMessagesAsync(conversation.Id, _options.HistoryMessages, cancellationToken);
        var prompt = _contextBuilder.Build(request.Message, searchResult, history);

        var answer = await _chat.CompleteAsync(prompt, cancellationToken);

        await PersistAssistantMessageAsync(conversation, answer, cancellationToken);
        await ExtractCandidatesAsync(conversation.Id, request.Message, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new ChatResponse
        {
            ConversationId = conversation.Id,
            Answer = answer,
            Intent = intent,
            Citations = BuildCitations(searchResult)
        };
    }

    public async IAsyncEnumerable<string> ChatStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var conversation = await _repository.GetOrCreateConversationAsync(request.ConversationId, request.UserId, cancellationToken);
        await PersistUserMessageAsync(conversation, request.Message, cancellationToken);

        var searchResult = await _search.SearchAsync(request.Message, cancellationToken);
        var history = await _repository.GetRecentMessagesAsync(conversation.Id, _options.HistoryMessages, cancellationToken);
        var prompt = _contextBuilder.Build(request.Message, searchResult, history);

        var full = new StringBuilder();
        await foreach (var token in _chat.CompleteStreamingAsync(prompt, cancellationToken))
        {
            full.Append(token);
            yield return token;
        }

        await PersistAssistantMessageAsync(conversation, full.ToString(), cancellationToken);
        await ExtractCandidatesAsync(conversation.Id, request.Message, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistUserMessageAsync(Conversation conversation, string message, CancellationToken cancellationToken)
    {
        await _repository.AddMessageAsync(new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Message = message
        }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistAssistantMessageAsync(Conversation conversation, string message, CancellationToken cancellationToken)
    {
        await _repository.AddMessageAsync(new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Message = message
        }, cancellationToken);
    }

    private async Task ExtractCandidatesAsync(Guid conversationId, string message, CancellationToken cancellationToken)
    {
        var extraction = await _extractor.ExtractAsync(message, cancellationToken);
        foreach (var fact in extraction.Facts.Where(f => f.Confidence >= _options.MinExtractionConfidence))
        {
            await _repository.AddCandidateAsync(new CandidateKnowledge
            {
                SourceConversationId = conversationId,
                ExtractedFact = fact.Text,
                Reason = fact.Reason,
                Confidence = fact.Confidence,
                Status = CandidateStatus.Pending
            }, cancellationToken);
        }
    }

    private static List<Citation> BuildCitations(SearchResult searchResult) =>
        searchResult.Hits
            .OrderByDescending(h => h.Score)
            .Take(6)
            .Select(h => new Citation(h.Chunk.Id, h.Chunk.Document?.Title, h.Chunk.Heading, Math.Round(h.Score, 4)))
            .ToList();
}

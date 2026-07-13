using Knowledge.Core.Domain;
using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Builds the final prompt sent to the LLM from retrieval results and history.</summary>
public interface IContextBuilder
{
    /// <summary>
    /// Assembles the message list (system prompt + knowledge + history + question) that
    /// will be sent to the chat model.
    /// </summary>
    IReadOnlyList<(MessageRole Role, string Content)> Build(
        string question,
        SearchResult searchResult,
        IReadOnlyList<ConversationMessage> history);
}

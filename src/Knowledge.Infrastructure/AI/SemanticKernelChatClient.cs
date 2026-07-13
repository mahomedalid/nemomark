using System.Runtime.CompilerServices;
using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Knowledge.Infrastructure.AI;

/// <summary>Wraps Semantic Kernel's chat completion service behind the domain abstraction.</summary>
public sealed class SemanticKernelChatClient : ILlmChatClient
{
    private readonly IChatCompletionService _chat;

    public SemanticKernelChatClient(IChatCompletionService chat) => _chat = chat;

    public async Task<string> CompleteAsync(
        IReadOnlyList<(MessageRole Role, string Content)> messages,
        CancellationToken cancellationToken = default)
    {
        var history = ToChatHistory(messages);
        var result = await _chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        return result.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        IReadOnlyList<(MessageRole Role, string Content)> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = ToChatHistory(messages);
        await foreach (var token in _chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(token.Content))
            {
                yield return token.Content;
            }
        }
    }

    private static ChatHistory ToChatHistory(IReadOnlyList<(MessageRole Role, string Content)> messages)
    {
        var history = new ChatHistory();
        foreach (var (role, content) in messages)
        {
            switch (role)
            {
                case MessageRole.System:
                    history.AddSystemMessage(content);
                    break;
                case MessageRole.Assistant:
                    history.AddAssistantMessage(content);
                    break;
                default:
                    history.AddUserMessage(content);
                    break;
            }
        }

        return history;
    }
}

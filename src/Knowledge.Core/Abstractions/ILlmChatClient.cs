using Knowledge.Core.Domain;

namespace Knowledge.Core.Abstractions;

/// <summary>Low-level chat completion abstraction over the LLM provider.</summary>
public interface ILlmChatClient
{
    /// <summary>Generates a single completion for the supplied messages.</summary>
    Task<string> CompleteAsync(
        IReadOnlyList<(MessageRole Role, string Content)> messages,
        CancellationToken cancellationToken = default);

    /// <summary>Streams a completion token-by-token.</summary>
    IAsyncEnumerable<string> CompleteStreamingAsync(
        IReadOnlyList<(MessageRole Role, string Content)> messages,
        CancellationToken cancellationToken = default);
}

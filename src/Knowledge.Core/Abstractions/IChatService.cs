using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Top-level chat orchestration service.</summary>
public interface IChatService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ChatStreamingAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

namespace Knowledge.Core.Abstractions;

/// <summary>
/// A knowledge-grounded conversational agent built on the Microsoft Agent Framework.
/// The agent decides when to call the knowledge base (via tools) to ground its answers.
/// </summary>
public interface IKnowledgeAgent
{
    /// <summary>Runs the agent for a single turn and returns the final text answer.</summary>
    Task<string> RunAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>Runs the agent and streams the response text as it is produced.</summary>
    IAsyncEnumerable<string> RunStreamingAsync(string message, CancellationToken cancellationToken = default);
}

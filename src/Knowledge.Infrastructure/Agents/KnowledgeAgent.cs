using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Knowledge.Core.Abstractions;
using Knowledge.Infrastructure.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Agents;

/// <summary>
/// A knowledge-grounded agent built on the Microsoft Agent Framework. Instead of statically
/// stuffing retrieved chunks into the prompt, the agent is given a <c>search_knowledge</c> tool
/// and decides when to query the knowledge base to ground its answers.
/// </summary>
public sealed class KnowledgeAgent : IKnowledgeAgent
{
    private readonly AIAgent _agent;
    private readonly IKnowledgeSearchService _search;
    private readonly KnowledgeOptions _options;

    public KnowledgeAgent(AIAgent agent, IKnowledgeSearchService search, IOptions<KnowledgeOptions> options)
    {
        _agent = agent;
        _search = search;
        _options = options.Value;
    }

    public async Task<string> RunAsync(string message, CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(message, options: BuildRunOptions(cancellationToken), cancellationToken: cancellationToken);
        return response.Text;
    }

    public async IAsyncEnumerable<string> RunStreamingAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _agent.RunStreamingAsync(message, options: BuildRunOptions(cancellationToken), cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    /// <summary>
    /// Builds per-run options that expose the knowledge-base search tool. The tool closes over the
    /// current cancellation token and the request-scoped search service.
    /// </summary>
    private ChatClientAgentRunOptions BuildRunOptions(CancellationToken cancellationToken)
    {
        var searchTool = AIFunctionFactory.Create(
            ([Description("A natural language search query.")] string query)
                => SearchKnowledgeAsync(query, cancellationToken),
            name: "search_knowledge",
            description: "Searches the organization's knowledge base and returns the most relevant "
                         + "snippets with citation indices. Call this whenever the user asks about "
                         + "facts, documentation, or domain knowledge.");

        return new ChatClientAgentRunOptions(new ChatOptions { Tools = [searchTool] });
    }

    private async Task<string> SearchKnowledgeAsync(string query, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(query, cancellationToken);
        if (result.Hits.Count == 0)
        {
            return "No relevant knowledge was found for that query.";
        }

        var builder = new StringBuilder();
        var index = 1;
        foreach (var hit in result.Hits.OrderByDescending(h => h.Score).Take(_options.ContextChunks))
        {
            var title = hit.Chunk.Document?.Title ?? "Knowledge";
            var heading = string.IsNullOrWhiteSpace(hit.Chunk.Heading) ? string.Empty : $" — {hit.Chunk.Heading}";
            builder.AppendLine($"[{index++}] ({title}{heading})");
            builder.AppendLine(hit.Chunk.Markdown);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
}

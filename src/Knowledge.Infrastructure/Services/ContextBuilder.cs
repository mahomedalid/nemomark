using System.Text;
using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Knowledge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Services;

/// <summary>
/// Builds the final prompt: system prompt + de-duplicated knowledge chunks (within a token
/// budget) + conversation history + the user question.
/// </summary>
public sealed class ContextBuilder : IContextBuilder
{
    private const int CharsPerToken = 4;

    private const string SystemPrompt =
        "You are a knowledge assistant. Answer the user's question using ONLY the provided knowledge " +
        "context. If the context does not contain the answer, say you don't have that information. " +
        "Be concise and accurate. Cite sources by their [n] index when helpful.";

    private readonly KnowledgeOptions _options;

    public ContextBuilder(IOptions<KnowledgeOptions> options) => _options = options.Value;

    public IReadOnlyList<(MessageRole Role, string Content)> Build(
        string question,
        SearchResult searchResult,
        IReadOnlyList<ConversationMessage> history)
    {
        var messages = new List<(MessageRole, string)>
        {
            (MessageRole.System, SystemPrompt)
        };

        var knowledge = BuildKnowledgeBlock(searchResult);
        if (!string.IsNullOrWhiteSpace(knowledge))
        {
            messages.Add((MessageRole.System, "Knowledge context:\n" + knowledge));
        }

        foreach (var message in history)
        {
            messages.Add((message.Role, message.Message));
        }

        messages.Add((MessageRole.User, question));
        return messages;
    }

    private string BuildKnowledgeBlock(SearchResult searchResult)
    {
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        var usedTokens = 0;
        var index = 1;

        foreach (var hit in searchResult.Hits.OrderByDescending(h => h.Score).Take(_options.ContextChunks))
        {
            var chunk = hit.Chunk;
            if (!seenHashes.Add(chunk.Hash))
            {
                continue;
            }

            var source = chunk.Document?.Title ?? chunk.Section ?? "Knowledge";
            var heading = string.IsNullOrWhiteSpace(chunk.Heading) ? string.Empty : $" — {chunk.Heading}";
            var entry = $"[{index}] ({source}{heading})\n{chunk.Markdown}\n";

            var entryTokens = entry.Length / CharsPerToken;
            if (usedTokens + entryTokens > _options.ContextTokenBudget && builder.Length > 0)
            {
                break;
            }

            builder.AppendLine(entry);
            usedTokens += entryTokens;
            index++;
        }

        return builder.ToString().Trim();
    }
}

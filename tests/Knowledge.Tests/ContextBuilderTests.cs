using Knowledge.Core.Abstractions;
using Knowledge.Core.Domain;
using Knowledge.Core.Models;
using Knowledge.Infrastructure.Configuration;
using Knowledge.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Knowledge.Tests;

public class ContextBuilderTests
{
    private static ContextBuilder CreateBuilder() =>
        new(Options.Create(new KnowledgeOptions { ContextChunks = 4, ContextTokenBudget = 10000 }));

    private static SearchResult MakeResult(params (string hash, double score)[] hits)
    {
        var searchHits = hits.Select(h => new SearchHit(
            new KnowledgeChunk
            {
                Hash = h.hash,
                Markdown = $"content-{h.hash}",
                Heading = "H",
                Document = new KnowledgeDocument { Title = "Doc" }
            },
            h.score)).ToList();
        return new SearchResult("q", searchHits);
    }

    [Fact]
    public void Build_IncludesSystemPromptAndQuestion()
    {
        var builder = CreateBuilder();

        var messages = builder.Build("What is X?", MakeResult(("a", 0.9)), Array.Empty<ConversationMessage>());

        Assert.Equal(MessageRole.System, messages[0].Role);
        Assert.Equal(MessageRole.User, messages[^1].Role);
        Assert.Equal("What is X?", messages[^1].Content);
    }

    [Fact]
    public void Build_DeduplicatesChunksByHash()
    {
        var builder = CreateBuilder();

        var messages = builder.Build("q", MakeResult(("dup", 0.9), ("dup", 0.8), ("other", 0.7)), Array.Empty<ConversationMessage>());

        var knowledge = messages.First(m => m.Content.StartsWith("Knowledge context:")).Content;
        var occurrences = knowledge.Split("content-dup").Length - 1;
        Assert.Equal(1, occurrences);
        Assert.Contains("content-other", knowledge);
    }

    [Fact]
    public void Build_IncludesHistory()
    {
        var builder = CreateBuilder();
        var history = new List<ConversationMessage>
        {
            new() { Role = MessageRole.User, Message = "earlier question" },
            new() { Role = MessageRole.Assistant, Message = "earlier answer" }
        };

        var messages = builder.Build("q", MakeResult(("a", 0.9)), history);

        Assert.Contains(messages, m => m.Content == "earlier question");
        Assert.Contains(messages, m => m.Content == "earlier answer");
    }
}

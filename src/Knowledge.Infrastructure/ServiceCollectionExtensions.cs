using Knowledge.Core.Abstractions;
using Knowledge.Infrastructure.AI;
using Knowledge.Infrastructure.Agents;
using Knowledge.Infrastructure.Configuration;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Infrastructure.Services;
using Knowledge.Ingestion;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Knowledge.Infrastructure;

public static class ServiceCollectionExtensions
{
    private const string AgentInstructions =
        "You are a knowledge assistant for the organization. Answer questions accurately and " +
        "concisely. Before answering questions about facts, documentation, or domain knowledge, " +
        "use the search_knowledge tool to retrieve grounding context, and base your answer on the " +
        "returned snippets. Cite sources using their [n] indices. If the knowledge base does not " +
        "contain the answer, say so clearly instead of guessing.";

    /// <summary>Registers the full knowledge chatbot service graph.</summary>
    public static IServiceCollection AddKnowledgeServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));
        services.Configure<KnowledgeOptions>(configuration.GetSection(KnowledgeOptions.SectionName));

        var aoai = configuration.GetSection(AzureOpenAiOptions.SectionName).Get<AzureOpenAiOptions>() ?? new AzureOpenAiOptions();
        var connectionString = configuration.GetConnectionString("KnowledgeDb")
            ?? throw new InvalidOperationException("Connection string 'KnowledgeDb' is not configured.");

        // Database
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        // Azure OpenAI via Semantic Kernel (chat and embedding may target different resources)
        services.AddAzureOpenAIChatCompletion(
            deploymentName: aoai.ChatDeployment,
            endpoint: aoai.ResolvedChatEndpoint,
            apiKey: aoai.ResolvedChatApiKey);

        services.AddAzureOpenAIEmbeddingGenerator(
            deploymentName: aoai.EmbeddingDeployment,
            endpoint: aoai.ResolvedEmbeddingEndpoint,
            apiKey: aoai.ResolvedEmbeddingApiKey,
            dimensions: aoai.EmbeddingDimensions);

        // AI adapters
        services.AddScoped<ILlmChatClient, SemanticKernelChatClient>();
        services.AddScoped<IEmbeddingService, AzureOpenAiEmbeddingService>();
        services.AddScoped<IMetadataEnricher, LlmMetadataEnricher>();
        services.AddScoped<IIntentClassifier, LlmIntentClassifier>();
        services.AddScoped<IKnowledgeExtractor, LlmKnowledgeExtractor>();

        // Ingestion pipeline
        services.AddScoped<IMarkdownParser, MarkdownParser>();
        services.AddScoped<IChunker, HeadingAwareChunker>();
        services.AddScoped<IIngestionService, IngestionService>();

        // Retrieval & chat
        services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();
        services.AddScoped<IContextBuilder, ContextBuilder>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IApprovalService, ApprovalService>();

        // Microsoft Agent Framework agent (tool-calling, knowledge-grounded)
        services.AddSingleton<AIAgent>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AzureOpenAiOptions>>().Value;
            var chatClient = new AzureOpenAIClient(new Uri(opts.ResolvedChatEndpoint), new ApiKeyCredential(opts.ResolvedChatApiKey))
                .GetChatClient(opts.ChatDeployment);
            return chatClient.AsAIAgent(instructions: AgentInstructions, name: "KnowledgeAgent");
        });
        services.AddScoped<IKnowledgeAgent, KnowledgeAgent>();

        return services;
    }
}

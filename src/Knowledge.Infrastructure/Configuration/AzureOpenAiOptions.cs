namespace Knowledge.Infrastructure.Configuration;

/// <summary>Azure OpenAI connection and deployment settings.</summary>
public class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Shared/fallback endpoint used when a model-specific endpoint is not set.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Shared/fallback API key used when a model-specific key is not set.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Deployment name of the chat completion model.</summary>
    public string ChatDeployment { get; set; } = "gpt-4o-mini";

    /// <summary>Endpoint of the chat resource. Falls back to <see cref="Endpoint"/> when empty.</summary>
    public string ChatEndpoint { get; set; } = string.Empty;

    /// <summary>API key of the chat resource. Falls back to <see cref="ApiKey"/> when empty.</summary>
    public string ChatApiKey { get; set; } = string.Empty;

    /// <summary>Deployment name of the embedding model (e.g. text-embedding-3-small).</summary>
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";

    /// <summary>Endpoint of the embedding resource. Falls back to <see cref="Endpoint"/> when empty.</summary>
    public string EmbeddingEndpoint { get; set; } = string.Empty;

    /// <summary>API key of the embedding resource. Falls back to <see cref="ApiKey"/> when empty.</summary>
    public string EmbeddingApiKey { get; set; } = string.Empty;

    /// <summary>Dimensionality of the embedding vectors.</summary>
    public int EmbeddingDimensions { get; set; } = 1536;

    /// <summary>Effective chat endpoint (model-specific if set, otherwise the shared endpoint).</summary>
    public string ResolvedChatEndpoint =>
        string.IsNullOrWhiteSpace(ChatEndpoint) ? Endpoint : ChatEndpoint;

    /// <summary>Effective chat API key (model-specific if set, otherwise the shared key).</summary>
    public string ResolvedChatApiKey =>
        string.IsNullOrWhiteSpace(ChatApiKey) ? ApiKey : ChatApiKey;

    /// <summary>Effective embedding endpoint (model-specific if set, otherwise the shared endpoint).</summary>
    public string ResolvedEmbeddingEndpoint =>
        string.IsNullOrWhiteSpace(EmbeddingEndpoint) ? Endpoint : EmbeddingEndpoint;

    /// <summary>Effective embedding API key (model-specific if set, otherwise the shared key).</summary>
    public string ResolvedEmbeddingApiKey =>
        string.IsNullOrWhiteSpace(EmbeddingApiKey) ? ApiKey : EmbeddingApiKey;
}

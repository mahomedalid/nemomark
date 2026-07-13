namespace Knowledge.Infrastructure.Configuration;

/// <summary>Retrieval and context assembly settings.</summary>
public class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    /// <summary>Number of chunks to retrieve from the vector store.</summary>
    public int TopK { get; set; } = 20;

    /// <summary>Number of chunks to keep after ranking for the prompt.</summary>
    public int ContextChunks { get; set; } = 6;

    /// <summary>Approximate token budget for the knowledge portion of the prompt.</summary>
    public int ContextTokenBudget { get; set; } = 3000;

    /// <summary>Number of prior conversation messages to include as history.</summary>
    public int HistoryMessages { get; set; } = 8;

    /// <summary>Directory that the worker watches for Markdown changes.</summary>
    public string KnowledgeDirectory { get; set; } = "knowledge";

    /// <summary>Minimum confidence required to store extracted candidate knowledge.</summary>
    public double MinExtractionConfidence { get; set; } = 0.6;
}

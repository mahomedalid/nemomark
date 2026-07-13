using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Retrieves the most relevant knowledge chunks for a question.</summary>
public interface IKnowledgeSearchService
{
    Task<SearchResult> SearchAsync(string question, CancellationToken cancellationToken = default);
}

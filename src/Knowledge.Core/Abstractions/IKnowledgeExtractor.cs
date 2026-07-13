using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>
/// Determines whether a message contains factual information worth storing as
/// candidate knowledge.
/// </summary>
public interface IKnowledgeExtractor
{
    Task<ExtractionResult> ExtractAsync(string message, CancellationToken cancellationToken = default);
}

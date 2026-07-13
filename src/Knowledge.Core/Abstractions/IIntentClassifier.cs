using Knowledge.Core.Models;

namespace Knowledge.Core.Abstractions;

/// <summary>Classifies the intent of a user message before retrieval.</summary>
public interface IIntentClassifier
{
    Task<IntentResult> ClassifyAsync(string message, CancellationToken cancellationToken = default);
}

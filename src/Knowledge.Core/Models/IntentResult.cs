using Knowledge.Core.Domain;

namespace Knowledge.Core.Models;

/// <summary>Result of classifying a user message's intent.</summary>
public record IntentResult(IntentType Intent, double Confidence);

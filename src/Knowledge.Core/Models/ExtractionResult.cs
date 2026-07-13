namespace Knowledge.Core.Models;

/// <summary>A single fact extracted from a conversation message.</summary>
public record ExtractedFact(string Text, string Reason, double Confidence);

/// <summary>The result of running the knowledge extractor over a message.</summary>
public record ExtractionResult(IReadOnlyList<ExtractedFact> Facts)
{
    public static readonly ExtractionResult Empty = new(Array.Empty<ExtractedFact>());

    public bool HasFacts => Facts.Count > 0;
}

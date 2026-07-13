namespace Knowledge.Core.Domain;

/// <summary>
/// A fact extracted from a conversation that is a candidate for inclusion in the
/// official knowledge base. Never merged automatically.
/// </summary>
public class CandidateKnowledge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? SourceConversationId { get; set; }

    public string ExtractedFact { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public double Confidence { get; set; }

    public CandidateStatus Status { get; set; } = CandidateStatus.Pending;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

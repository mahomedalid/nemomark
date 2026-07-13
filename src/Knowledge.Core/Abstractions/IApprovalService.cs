using Knowledge.Core.Domain;

namespace Knowledge.Core.Abstractions;

/// <summary>Manages the candidate knowledge approval workflow.</summary>
public interface IApprovalService
{
    Task<IReadOnlyList<CandidateKnowledge>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Approves a candidate, promoting it into an official knowledge chunk.</summary>
    Task<bool> ApproveAsync(Guid candidateId, CancellationToken cancellationToken = default);

    Task<bool> RejectAsync(Guid candidateId, CancellationToken cancellationToken = default);
}

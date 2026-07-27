using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

/// <summary>
/// Review status of a compiled grading contract. After ingestion the contract is
/// <see cref="Approved"/> automatically; <see cref="Pending"/> is legacy, and
/// <see cref="Rejected"/> marks an admin-invalidated contract.
/// </summary>
public enum GradingContractStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// The authoritative compiled rubric (check-items + partial-credit tiers + answer keys) for one
/// subject / rubric version, produced by the AIGradingService ingestion pipeline and stored as JSON.
/// Versioned by <see cref="SubjectId"/> + <see cref="RubricVersion"/>; illustration crops referenced
/// by the contract are stored separately as <see cref="RubricAsset"/> rows.
/// </summary>
public class GradingContract : Entity
{
    public Guid SubjectId { get; set; }

    public int RubricVersion { get; set; } = 1;

    /// <summary>Serialized <c>CompiledContract</c> (criteria, blocks, warnings, asset metadata).</summary>
    public string ContractJson { get; set; } = "{}";

    public bool CoverageOk { get; set; }

    public string? ModelUsed { get; set; }

    public GradingContractStatus Status { get; set; } = GradingContractStatus.Pending;

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }
}

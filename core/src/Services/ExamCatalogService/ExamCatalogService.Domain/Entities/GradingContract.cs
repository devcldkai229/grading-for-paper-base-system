using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

/// <summary>
/// Review status of a compiled grading contract. A contract is <see cref="Pending"/> after the AI
/// ingestion pipeline produces it, becomes <see cref="Approved"/> once an admin reviews it (the hard
/// gate before it can be used for AI grading), or <see cref="Rejected"/> if unusable.
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

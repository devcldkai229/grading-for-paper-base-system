using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

/// <summary>
/// An illustration/table/formula cropped from a barem during ingestion and stored in S3, so a
/// criterion that <c>requiresVisual</c> can re-show it to the grader. Versioned alongside the
/// <see cref="GradingContract"/> by <see cref="SubjectId"/> + <see cref="RubricVersion"/>.
/// </summary>
public class RubricAsset : Entity
{
    public Guid SubjectId { get; set; }

    public int RubricVersion { get; set; } = 1;

    /// <summary>Logical id referenced by the contract's <c>visualAssetIds</c> (e.g. "a0").</summary>
    public string AssetId { get; set; } = string.Empty;

    public string Kind { get; set; } = "illustration";

    public string? Question { get; set; }

    public int PageIndex { get; set; }

    public string S3Key { get; set; } = string.Empty;

    public string ContentType { get; set; } = "image/png";
}

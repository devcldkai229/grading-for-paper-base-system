using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Domain.Entities;

namespace ExamCatalogService.Application.Interfaces;

public interface IExamCatalogRepository
{
    Task<(IReadOnlyList<SemesterDto> Items, int TotalCount)> GetSemestersAsync(
        bool? active, int page, int pageSize, CancellationToken ct = default);

    Task<SemesterDto?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default);

    Task<(IReadOnlyList<ExamDto> Items, int TotalCount)> GetExamsBySemesterAsync(
        Guid semesterId, int page, int pageSize, CancellationToken ct = default);

    Task<ExamDto?> GetExamByIdAsync(Guid examId, CancellationToken ct = default);

    Task<(IReadOnlyList<SubjectSummaryDto> Items, int TotalCount)> GetSubjectsByExamAsync(
        Guid examId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Cross-exam subject search by code (substring, case-insensitive), semester, exam and/or status.
    /// When <paramref name="restrictToSubjectIds"/> is non-null, results are additionally limited to
    /// that set (used to scope a Lecturer to only the subjects they're assigned to grade).
    /// </summary>
    Task<(IReadOnlyList<SubjectSearchResultDto> Items, int TotalCount)> SearchSubjectsAsync(
        string? code,
        Guid? semesterId,
        Guid? examId,
        Domain.Enums.SubjectStatus? status,
        IReadOnlySet<Guid>? restrictToSubjectIds,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<SubjectDetailDto?> GetSubjectDetailAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Distinct subject ids that <paramref name="lecturerId"/> is assigned to grade, read from the
    /// local marker_assignment_view projection (kept in sync via MarkerAssignmentChanged events).
    /// Replaces the former synchronous call to GradingService — no availability coupling (N5).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAssignedSubjectIdsAsync(
        Guid lecturerId, CancellationToken ct = default);

    /// <summary>
    /// Minimal exam context for a subject — used by GradingService (internal, service-to-service)
    /// to surface upcoming grading deadlines without exposing the full subject/exam graph.
    /// </summary>
    Task<SubjectExamInfoDto?> GetSubjectExamInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns preview (s3Key, displayFileName, previewContentType, originalContentType) for inline viewing.
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType, string OriginalContentType)?> GetExamPaperViewInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns original (s3Key, fileName, contentType) for DOCX download.
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType)?> GetExamPaperOriginalInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns (s3Key, fileName, contentType) for exam paper original file (AI extract).
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType)?> GetExamPaperInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns preview info for inline rubric viewing.
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType, string OriginalContentType, int RubricVersion)?> GetRubricViewInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns original rubric file for DOCX download.
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType, int RubricVersion)?> GetRubricOriginalInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns (s3Key, fileName, contentType, rubricVersion) for rubric original file (AI extract).
    /// </summary>
    Task<(string S3Key, string FileName, string ContentType, int RubricVersion)?> GetRubricInfoAsync(
        Guid subjectId, CancellationToken ct = default);

    Task<SemesterDto> CreateSemesterAsync(CreateSemesterRequest request, CancellationToken ct = default);

    Task<SemesterDto?> UpdateSemesterAsync(
        Guid semesterId, UpdateSemesterRequest request, CancellationToken ct = default);

    Task<bool> DeleteSemesterAsync(Guid semesterId, CancellationToken ct = default);

    Task<ExamDto> CreateExamAsync(CreateExamRequest request, CancellationToken ct = default);

    Task<ExamDto?> UpdateExamAsync(Guid examId, UpdateExamRequest request, CancellationToken ct = default);

    Task<SubjectDetailDto> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default);

    Task<SubjectDetailDto?> UpdateSubjectAsync(
        Guid subjectId, UpdateSubjectRequest request, CancellationToken ct = default);

    Task<bool> UpdateExamPaperAsync(
        Guid subjectId,
        string s3Key,
        string fileName,
        string contentType,
        string previewS3Key,
        string previewContentType,
        CancellationToken ct = default);

    Task<int?> UpdateRubricAsync(
        Guid subjectId,
        string s3Key,
        string fileName,
        string contentType,
        string previewS3Key,
        string previewContentType,
        CancellationToken ct = default);

    Task<IReadOnlyList<QuestionDto>?> ReplaceQuestionsAsync(
        Guid subjectId, IReadOnlyList<QuestionInputDto> questions, CancellationToken ct = default);

    // ── Compiled grading contracts ──

    /// <summary>Insert or replace the compiled contract + assets for a subject/rubric version.</summary>
    Task UpsertGradingContractAsync(
        GradingContract contract, IReadOnlyList<RubricAsset> assets, CancellationToken ct = default);

    Task<GradingContract?> GetGradingContractAsync(
        Guid subjectId, int rubricVersion, CancellationToken ct = default);

    Task<GradingContract?> GetGradingContractByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Latest APPROVED contract for a subject (highest rubric version), or null.</summary>
    Task<GradingContract?> GetApprovedGradingContractAsync(
        Guid subjectId, int? rubricVersion, CancellationToken ct = default);

    Task<IReadOnlyList<GradingContract>> ListGradingContractsAsync(
        GradingContractStatus? status, CancellationToken ct = default);

    Task<IReadOnlyList<RubricAsset>> GetRubricAssetsAsync(
        Guid subjectId, int rubricVersion, CancellationToken ct = default);

    /// <summary>Set review status (and optionally overwrite the edited contract JSON on approval).</summary>
    Task<bool> UpdateGradingContractReviewAsync(
        Guid id, GradingContractStatus status, Guid reviewedBy, string? contractJson,
        CancellationToken ct = default);
}

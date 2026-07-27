using GradingService.Domain.Entities;
using GradingService.Domain.Enums;

namespace GradingService.Application.Interfaces;

public interface IGradingAssignmentRepository
{
    /// <summary>
    /// Loads an assignment with its GradingForm and QuestionGradeDetails.
    /// Pass <paramref name="teacherId"/> to scope to a specific marker (null = any marker, admin use).
    /// </summary>
    Task<GradingAssignment?> GetWithFormAndDetailsAsync(
        Guid assignmentId, Guid? teacherId, bool asNoTracking, CancellationToken ct = default);

    /// <summary>
    /// Loads an assignment with its GradingForm only (no QuestionGradeDetails).
    /// Pass <paramref name="teacherId"/> to scope to a specific marker (null = any marker, admin use).
    /// </summary>
    Task<GradingAssignment?> GetWithFormAsync(
        Guid assignmentId, Guid? teacherId, bool asNoTracking, CancellationToken ct = default);

    /// <summary>Finds the FirstGrade assignment (with form + details, tracked) for a paper/teacher pair, if any.</summary>
    Task<GradingAssignment?> FindFirstGradeForPaperAsync(
        Guid studentPaperId, Guid teacherId, CancellationToken ct = default);

    /// <summary>Lists (untracked) assignments for a teacher restricted to a set of paper ids.</summary>
    Task<IReadOnlyList<GradingAssignment>> ListByTeacherAndPaperIdsAsync(
        Guid teacherId, IReadOnlyCollection<Guid> paperIds, CancellationToken ct = default);

    /// <summary>Lists (untracked, with form + details) assignments for a subject, optionally restricted to Submitted ones.</summary>
    Task<IReadOnlyList<GradingAssignment>> ListBySubjectWithFormsAsync(
        Guid subjectId, bool submittedOnly, CancellationToken ct = default);

    /// <summary>Lists (untracked, with form + details) all assignments owned by a teacher.</summary>
    Task<IReadOnlyList<GradingAssignment>> ListByTeacherWithFormsAsync(
        Guid teacherId, CancellationToken ct = default);

    Task<IReadOnlyList<AssignmentProgressRow>> ListProgressByTeacherAsync(
        Guid teacherId, CancellationToken ct = default);

    Task<IReadOnlyList<SubjectProgressRow>> ListProgressRowsAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);

    Task<IReadOnlyList<SubjectScoreRow>> ListSubmittedScoresAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);

    /// <summary>Lists (untracked) a teacher's own assignments, optionally filtered by status and/or
    /// flagged state. Not paginated/ordered here — the caller joins in alias data and paginates
    /// afterward (see GradingSessionService.GetGradingQueueAsync).</summary>
    Task<IReadOnlyList<GradingQueueRow>> ListQueueRowsAsync(
        Guid teacherId, GradingProgressStatus? status, bool? flaggedOnly, CancellationToken ct = default);

    /// <summary>Stages a new assignment for insertion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Add(GradingAssignment assignment);

    /// <summary>Stages a new grading form for insertion (legacy-data fallback path).</summary>
    void AddForm(GradingForm form);

    /// <summary>Removes a grading assignment (cascades to form + details).</summary>
    void Remove(GradingAssignment assignment);
}

public sealed record AssignmentProgressRow(Guid Id, Guid SubjectId, GradingProgressStatus Status, DateTime CreatedAt);

public sealed record SubjectProgressRow(
    Guid SubjectId, Guid TeacherId, GradingProgressStatus Status, DateTime? SubmittedAt, decimal? TotalScore,
    int ActiveSecondsSpent, bool IsFlagged);

public sealed record SubjectScoreRow(Guid SubjectId, decimal Score);

public sealed record GradingQueueRow(
    Guid AssignmentId, Guid StudentPaperId, Guid SubjectId, GradingProgressStatus Status,
    bool IsFlagged, decimal? TotalScore, DateTime? SubmittedAt, DateTime CreatedAt);

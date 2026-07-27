using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

/// <summary>
/// A reusable, subject-agnostic score grid (question/criteria structure) an Admin can save and
/// later apply to any subject. Applying a template just copies its questions into a subject's own
/// grid via the existing ReplaceQuestions flow — once applied, the rows are ordinary subject
/// questions with no lingering link back to the template.
/// </summary>
public class ScoreGridTemplate : Entity
{
    public string Name { get; set; } = string.Empty;

    public Guid CreatedBy { get; set; }

    /// <summary>Serialized <c>QuestionInputDto[]</c> — the template's rows (no SubjectId).</summary>
    public string QuestionsJson { get; set; } = "[]";

    /// <summary>Denormalized count of rows in <see cref="QuestionsJson"/>, so listing templates
    /// never needs to deserialize the JSON payload.</summary>
    public int QuestionCount { get; set; }
}

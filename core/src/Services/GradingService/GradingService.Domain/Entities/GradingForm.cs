using Contracts.Domain;

namespace GradingService.Domain.Entities;

public class GradingForm : Entity
{
    public Guid GradingAssignmentId { get; set; }

    public decimal TotalScore { get; set; }

    public string? PaperComment { get; set; }

    public string? GeneralComment { get; set; }

    public string? InternalComment { get; set; }

    /// <summary>
    /// AI grading suggestions kept SEPARATE from the manual marks. Serialized list of
    /// <c>AiSuggestionDto</c> ({questionNumber, score, questionComment, confidence, isManualOnly}).
    /// The lecturer copies these into the manual marks explicitly; the AI path never mutates
    /// <see cref="QuestionGradeDetails"/> or <see cref="PaperComment"/>.
    /// </summary>
    public string AiSuggestionsJson { get; set; } = "[]";

    /// <summary>AI-generated overall paper comment ("nhận xét bài"), shown in the AI suggestions tab.</summary>
    public string? AiPaperComment { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public int RowVersion { get; set; }

    /// <summary>Cumulative active (non-idle) seconds spent grading this paper, fed by client heartbeats.</summary>
    public int ActiveSecondsSpent { get; set; }

    /// <summary>Timestamp of the most recent accepted heartbeat, for troubleshooting/observability.</summary>
    public DateTime? LastActiveAt { get; set; }

    public GradingAssignment GradingAssignment { get; set; } = null!;

    public ICollection<QuestionGradeDetail> QuestionGradeDetails { get; set; } = new List<QuestionGradeDetail>();
}

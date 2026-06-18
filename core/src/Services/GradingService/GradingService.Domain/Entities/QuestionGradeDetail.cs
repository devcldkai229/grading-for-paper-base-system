using Contracts.Domain;
using GradingService.Domain.Enums;

namespace GradingService.Domain.Entities;

public class QuestionGradeDetail : Entity
{
    public Guid GradingFormId { get; set; }

    public string QuestionNumber { get; set; } = string.Empty;

    public decimal Score { get; set; }

    public decimal MaxScore { get; set; }

    public string? QuestionComment { get; set; }

    public bool AiDrafted { get; set; }

    public AiReviewStatus AiReviewStatus { get; set; } = AiReviewStatus.Pending;

    public GradingForm GradingForm { get; set; } = null!;
}

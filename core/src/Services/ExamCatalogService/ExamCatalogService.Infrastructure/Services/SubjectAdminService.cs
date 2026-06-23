using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;

namespace ExamCatalogService.Infrastructure.Services;

public class SubjectAdminService : ISubjectAdminService
{
    private const int MaxQuestionNumberLength = 20;
    private const int MaxGroupLabelLength = 100;
    private const int MaxLabelLength = 255;

    public (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateQuestions(
        decimal subjectMaxScore,
        IReadOnlyList<QuestionInputDto> questions)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (questions.Count == 0)
        {
            errors.Add("At least one question is required.");
            return (errors, warnings);
        }

        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal totalMax = 0;

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionNumber))
            {
                errors.Add("Each question must have a questionNumber.");
                continue;
            }

            var questionNumber = question.QuestionNumber.Trim();
            if (questionNumber.Length > MaxQuestionNumberLength)
            {
                warnings.Add(
                    $"Question {questionNumber[..MaxQuestionNumberLength]}…: questionNumber exceeds {MaxQuestionNumberLength} characters and will be truncated on save.");
            }

            if (!string.IsNullOrWhiteSpace(question.GroupLabel)
                && question.GroupLabel.Trim().Length > MaxGroupLabelLength)
            {
                warnings.Add(
                    $"Question {questionNumber}: groupLabel exceeds {MaxGroupLabelLength} characters and will be truncated on save.");
            }

            if (!string.IsNullOrWhiteSpace(question.Label)
                && question.Label.Length > MaxLabelLength)
            {
                warnings.Add(
                    $"Question {questionNumber}: label exceeds {MaxLabelLength} characters and will be truncated on save.");
            }

            if (!seenNumbers.Add(questionNumber))
            {
                errors.Add($"Duplicate questionNumber: {question.QuestionNumber}");
            }

            if (question.MaxScore <= 0)
            {
                errors.Add($"Question {question.QuestionNumber}: maxScore must be greater than 0.");
            }
            else
            {
                totalMax += question.MaxScore;
            }
        }

        if (errors.Count == 0 && totalMax != subjectMaxScore)
        {
            warnings.Add(
                $"Sum of question maxScore ({totalMax}) does not match subject maxScore ({subjectMaxScore}).");
        }

        return (errors, warnings);
    }
}

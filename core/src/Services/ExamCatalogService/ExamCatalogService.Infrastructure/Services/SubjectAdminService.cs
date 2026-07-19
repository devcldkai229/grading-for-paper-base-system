using System.Globalization;
using System.Text.RegularExpressions;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Infrastructure.Services;

public class SubjectAdminService : ISubjectAdminService
{
    private const int MaxQuestionNumberLength = 20;
    private const int MaxGroupLabelLength = 100;
    private const int MaxLabelLength = 255;

    /// <summary>Matches a percentage embedded in a group label, e.g. "Request 1 (20%)" -> 20.</summary>
    private static readonly Regex GroupPercentPattern = new(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled);

    /// <summary>The only forward move allowed from each status. Draft -> Open -> Grading -> Closed;
    /// Closed has no entry (terminal). Backward moves and skips are always rejected.</summary>
    private static readonly Dictionary<SubjectStatus, SubjectStatus> AllowedNextStatus = new()
    {
        [SubjectStatus.Draft] = SubjectStatus.Open,
        [SubjectStatus.Open] = SubjectStatus.Grading,
        [SubjectStatus.Grading] = SubjectStatus.Closed,
    };

    public string? ValidateStatusTransition(SubjectStatus current, SubjectStatus requested)
    {
        if (current == requested) return null;

        if (AllowedNextStatus.TryGetValue(current, out var next) && next == requested)
        {
            return null;
        }

        return $"Invalid status transition: {current} -> {requested}. " +
               "Subjects can only move forward one step at a time: Draft -> Open -> Grading -> Closed.";
    }

    public (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateQuestions(
        decimal subjectMaxScore,
        IReadOnlyList<QuestionInputDto> questions)
    {
        var (errors, warnings, totalMax) = ValidateQuestionStructure(questions);

        // Both checks below are independent facts about the same submission, so both run (and
        // can both report) as long as the per-question data itself is sound — a bad total
        // shouldn't hide a bad group, or vice versa.
        var hasFundamentalErrors = errors.Count > 0;

        if (!hasFundamentalErrors && totalMax != subjectMaxScore)
        {
            errors.Add(
                $"Sum of question maxScore ({totalMax}) does not match subject maxScore ({subjectMaxScore}).");
        }

        if (!hasFundamentalErrors)
        {
            // Sub-criteria budget check: a group label may declare its own budget as a percentage
            // of the subject's max score (e.g. "Request 1 (20%)"). When it does, that group's
            // leaves must sum to exactly that share. Groups with no parseable percentage have no
            // declared budget and are skipped here — group labels remain optional/free text.
            var groups = questions
                .Where(q => !string.IsNullOrWhiteSpace(q.GroupLabel))
                .GroupBy(q => q.GroupLabel!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var match = GroupPercentPattern.Match(group.Key);
                if (!match.Success) continue;

                var percent = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var expectedBudget = Math.Round(subjectMaxScore * percent / 100m, 2);
                var groupSum = Math.Round(group.Sum(q => q.MaxScore), 2);

                if (groupSum != expectedBudget)
                {
                    errors.Add(
                        $"Group '{group.Key}': sub-criteria sum to {groupSum} but the declared budget is {expectedBudget} ({percent}% of {subjectMaxScore}).");
                }
            }
        }

        return (errors, warnings);
    }

    /// <summary>
    /// Structural-only validation for a score-grid template: a template isn't tied to any one
    /// subject, so it has no max score to check leaf sums or group budgets against — those checks
    /// only make sense once the template's rows are applied to a specific subject (at which point
    /// the normal ValidateQuestions path, run via ReplaceQuestions, covers them).
    /// </summary>
    public (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateTemplateQuestions(
        IReadOnlyList<QuestionInputDto> questions)
    {
        var (errors, warnings, _) = ValidateQuestionStructure(questions);
        return (errors, warnings);
    }

    /// <summary>Per-row structural checks shared by ValidateQuestions and ValidateTemplateQuestions:
    /// required questionNumber, length limits, duplicate numbers, and maxScore > 0. Also returns the
    /// running sum of valid (maxScore > 0) rows' MaxScore, for callers that need it.</summary>
    private (List<string> Errors, List<string> Warnings, decimal TotalMax) ValidateQuestionStructure(
        IReadOnlyList<QuestionInputDto> questions)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (questions.Count == 0)
        {
            errors.Add("At least one question is required.");
            return (errors, warnings, 0);
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

        return (errors, warnings, totalMax);
    }
}

using System.Collections.Generic;
using System.Linq;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Infrastructure.Services;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class SubjectAdminServiceTests
    {
        private readonly SubjectAdminService _service = new();

        private static QuestionInputDto Q(string number, decimal maxScore, string? groupLabel = null, int orderIndex = 0) =>
            new(groupLabel, number, null, maxScore, orderIndex);

        [Fact]
        public void ValidateQuestions_WithNoQuestions_ReturnsError()
        {
            var (errors, warnings) = _service.ValidateQuestions(10m, new List<QuestionInputDto>());

            Assert.Contains(errors, e => e.Contains("At least one question"));
            Assert.Empty(warnings);
        }

        [Fact]
        public void ValidateQuestions_WithMaxScoreZeroOrNegative_ReturnsError()
        {
            var questions = new List<QuestionInputDto> { Q("1", 0m), Q("2", -5m) };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Contains(errors, e => e.Contains("Question 1") && e.Contains("maxScore must be greater than 0"));
            Assert.Contains(errors, e => e.Contains("Question 2") && e.Contains("maxScore must be greater than 0"));
        }

        [Fact]
        public void ValidateQuestions_WithDuplicateQuestionNumbers_ReturnsError()
        {
            var questions = new List<QuestionInputDto> { Q("1", 5m), Q("1", 5m) };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Contains(errors, e => e.Contains("Duplicate questionNumber"));
        }

        [Fact]
        public void ValidateQuestions_WhenSumMatchesSubjectMaxScore_NoTotalError()
        {
            var questions = new List<QuestionInputDto> { Q("1", 5m), Q("2", 5m) };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateQuestions_WhenSumDoesNotMatchSubjectMaxScore_ReturnsErrorNotWarning()
        {
            var questions = new List<QuestionInputDto> { Q("1", 5m), Q("2", 3m) };

            var (errors, warnings) = _service.ValidateQuestions(10m, questions);

            Assert.Contains(errors, e => e.Contains("Sum of question maxScore"));
            Assert.DoesNotContain(warnings, w => w.Contains("Sum of question maxScore"));
        }

        [Fact]
        public void ValidateQuestions_WhenGroupLabelHasNoPercentage_SkipsGroupBudgetCheck()
        {
            // "Yêu cầu 1" carries no parseable percentage, so its leaves aren't checked against
            // any implied budget — only the overall total (which matches here) is validated.
            var questions = new List<QuestionInputDto>
            {
                Q("1", 3m, "Yêu cầu 1"),
                Q("2", 7m, "Yêu cầu 1"),
            };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateQuestions_WhenGroupLeavesMatchDeclaredPercentBudget_NoError()
        {
            // "Request 1 (20%)" of a 10-point subject => budget 2.0; leaves sum to 2.0.
            var questions = new List<QuestionInputDto>
            {
                Q("1", 1m, "Request 1 (20%)"),
                Q("2", 1m, "Request 1 (20%)"),
                Q("3", 8m, "Request 2 (80%)"),
            };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateQuestions_WhenGroupLeavesDoNotMatchDeclaredPercentBudget_ReturnsError()
        {
            // "Request 1 (20%)" of a 10-point subject => budget 2.0, but leaves sum to 3.0.
            var questions = new List<QuestionInputDto>
            {
                Q("1", 2m, "Request 1 (20%)"),
                Q("2", 1m, "Request 1 (20%)"),
                Q("3", 7m, "Request 2 (80%)"),
            };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Contains(errors, e =>
                e.Contains("Request 1 (20%)") && e.Contains("3") && e.Contains("2"));
        }

        [Fact]
        public void ValidateQuestions_GroupLabelMatchingIsCaseInsensitiveAndTrimmed()
        {
            var questions = new List<QuestionInputDto>
            {
                Q("1", 1m, " Request 1 (20%) "),
                Q("2", 1m, "request 1 (20%)"),
                Q("3", 8m, "Request 2 (80%)"),
            };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateQuestions_WhenFundamentalErrorsExist_SkipsGroupBudgetCheck()
        {
            // A maxScore<=0 error makes the sums meaningless, so the group-budget check is
            // skipped rather than piling on a confusing secondary error.
            var questions = new List<QuestionInputDto>
            {
                Q("1", 0m, "Request 1 (20%)"),
                Q("2", 8m, "Request 2 (80%)"),
            };

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Single(errors);
            Assert.Contains(errors, e => e.Contains("maxScore must be greater than 0"));
        }

        [Fact]
        public void ValidateQuestions_ReportsBothTotalMismatchAndGroupMismatchTogether()
        {
            var questions = new List<QuestionInputDto>
            {
                Q("1", 5m, "Request 1 (20%)"), // budget should be 2.0, actual 5.0
                Q("2", 8m, "Request 2 (80%)"), // budget should be 8.0, actual 8.0 (fine)
            };
            // Total = 13, subject max = 10 -> mismatch too.

            var (errors, _) = _service.ValidateQuestions(10m, questions);

            Assert.Contains(errors, e => e.Contains("Sum of question maxScore"));
            Assert.Contains(errors, e => e.Contains("Request 1 (20%)"));
            Assert.Equal(2, errors.Count);
        }
    }
}

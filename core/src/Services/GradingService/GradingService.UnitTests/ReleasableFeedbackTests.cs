using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Application.Services;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Files;
using GradingService.Infrastructure.Persistence;
using GradingService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace GradingService.UnitTests
{
    public class ReleasableFeedbackTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingAssignment SeedAssignment(
            GradingDbContext db,
            Guid subjectId,
            GradingProgressStatus status,
            decimal totalScore,
            string? paperComment,
            string? internalComment)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = Guid.NewGuid(),
                Status = status
            };
            var form = new GradingForm
            {
                GradingAssignmentId = assignment.Id,
                TotalScore = totalScore,
                PaperComment = paperComment,
                InternalComment = internalComment,
                RowVersion = 1
            };
            form.QuestionGradeDetails.Add(new QuestionGradeDetail
            {
                GradingFormId = form.Id,
                QuestionNumber = "Q1",
                OrderIndex = 0,
                Score = totalScore,
                MaxScore = 10m,
                QuestionComment = "good work"
            });
            assignment.GradingForm = form;
            db.GradingAssignments.Add(assignment);
            return assignment;
        }

        private static GradingSessionService NewService(
            GradingDbContext db, ISubmissionServiceClient? submissionClient = null) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            submissionClient ?? Substitute.For<ISubmissionServiceClient>(),
            Substitute.For<IExamCatalogServiceClient>(),
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db));

        [Fact]
        public async Task GetReleasableFeedbackAsync_OnlyIncludesSubmittedAssignments()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, GradingProgressStatus.Submitted, 8m, "well done", "watch for plagiarism");
            SeedAssignment(db, subjectId, GradingProgressStatus.Drafting, 5m, "in progress", null);
            SeedAssignment(db, subjectId, GradingProgressStatus.NotStarted, 0m, null, null);
            await db.SaveChangesAsync();

            var service = NewService(db);
            var result = await service.GetReleasableFeedbackAsync(subjectId, CancellationToken.None);

            Assert.Single(result.Students);
            Assert.Equal(8m, result.Students[0].TotalScore);
        }

        [Fact]
        public async Task GetReleasableFeedbackAsync_NeverExposesInternalComment()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, GradingProgressStatus.Submitted, 9m, "great job", "TOP SECRET internal note");
            await db.SaveChangesAsync();

            var service = NewService(db);
            var result = await service.GetReleasableFeedbackAsync(subjectId, CancellationToken.None);

            // StudentFeedbackDto has no InternalComment property at all — structurally impossible to leak it.
            var studentDtoProperties = typeof(StudentFeedbackDto).GetProperties().Select(p => p.Name);
            Assert.DoesNotContain("InternalComment", studentDtoProperties);
            Assert.Equal("great job", result.Students[0].PaperComment);
        }

        [Fact]
        public async Task GetReleasableFeedbackAsync_IncludesPerQuestionScoresAndComments()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, GradingProgressStatus.Submitted, 8m, "ok", null);
            await db.SaveChangesAsync();

            var service = NewService(db);
            var result = await service.GetReleasableFeedbackAsync(subjectId, CancellationToken.None);

            var question = Assert.Single(result.Students[0].Questions);
            Assert.Equal("Q1", question.QuestionNumber);
            Assert.Equal("good work", question.QuestionComment);
        }

        [Fact]
        public async Task GetReleasableFeedbackAsync_EnrichesWithAliasFromSubmissionService()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var assignment = SeedAssignment(db, subjectId, GradingProgressStatus.Submitted, 7m, "fine", null);
            await db.SaveChangesAsync();

            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, Arg.Any<CancellationToken>())
                .Returns(new InternalPaperSummaryClientDto(
                    assignment.StudentPaperId, Guid.NewGuid(), subjectId, "Student_0007", 7, Guid.NewGuid()));

            var service = NewService(db, submissionClient);
            var result = await service.GetReleasableFeedbackAsync(subjectId, CancellationToken.None);

            Assert.Equal("Student_0007", result.Students[0].StudentAlias);
            Assert.Equal(7, result.Students[0].AliasNumber);
        }

        [Fact]
        public async Task GetReleasableFeedbackAsync_NoSubmittedAssignments_ReturnsEmptyList()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, GradingProgressStatus.NotStarted, 0m, null, null);
            await db.SaveChangesAsync();

            var service = NewService(db);
            var result = await service.GetReleasableFeedbackAsync(subjectId, CancellationToken.None);

            Assert.Empty(result.Students);
        }
    }
}

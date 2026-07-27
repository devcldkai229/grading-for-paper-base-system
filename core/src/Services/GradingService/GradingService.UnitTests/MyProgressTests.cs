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
    public class MyProgressTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingSessionService NewService(
            GradingDbContext db, IExamCatalogServiceClient catalogClient) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            Substitute.For<ISubmissionServiceClient>(),
            catalogClient,
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());

        private static GradingAssignment MakeAssignment(
            Guid teacherId, Guid subjectId, GradingProgressStatus status, DateTime createdAt)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = status
            };
            return assignment;
        }

        [Fact]
        public async Task GetMyProgressAsync_CountsTotalSubmittedAndRemaining()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, DateTime.UtcNow),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Drafting, DateTime.UtcNow),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns((SubjectExamInfoClientDto?)null);
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Equal(3, result.TotalAssignments);
            Assert.Equal(1, result.SubmittedCount);
            Assert.Equal(2, result.RemainingCount);
        }

        [Fact]
        public async Task GetMyProgressAsync_OnlyCountsCallingTeachersOwnAssignments()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var otherTeacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, DateTime.UtcNow),
                MakeAssignment(otherTeacherId, subjectId, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((SubjectExamInfoClientDto?)null);
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Equal(1, result.TotalAssignments);
            Assert.Equal(1, result.SubmittedCount);
        }

        [Fact]
        public async Task GetMyProgressAsync_ExcludesFullyGradedSubjectsFromDeadlines()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var doneSubjectId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, doneSubjectId, GradingProgressStatus.Submitted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Empty(result.UpcomingDeadlines);
            // Fully-graded subject must not even trigger a lookup — it has no remaining work.
            await catalogClient.DidNotReceive().GetExamInfoAsync(doneSubjectId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetMyProgressAsync_ExcludesSubjectsWithoutAnExamDeadline()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectExamInfoClientDto(subjectId, "PRN222", Guid.NewGuid(), "FE Exam", null));
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Empty(result.UpcomingDeadlines);
            // No deadline info anywhere => fall back to this subject's next assignment.
            Assert.NotNull(result.NextAssignmentId);
        }

        [Fact]
        public async Task GetMyProgressAsync_SortsUpcomingDeadlinesBySoonestFirst()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectSoon = Guid.NewGuid();
            var subjectLater = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectSoon, GradingProgressStatus.NotStarted, DateTime.UtcNow),
                MakeAssignment(teacherId, subjectLater, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(subjectSoon, Arg.Any<CancellationToken>())
                .Returns(new SubjectExamInfoClientDto(subjectSoon, "SOON", Guid.NewGuid(), "Exam A", today.AddDays(10)));
            catalogClient.GetExamInfoAsync(subjectLater, Arg.Any<CancellationToken>())
                .Returns(new SubjectExamInfoClientDto(subjectLater, "LATER", Guid.NewGuid(), "Exam B", today.AddDays(2)));
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Equal(2, result.UpcomingDeadlines.Count);
            Assert.Equal("LATER", result.UpcomingDeadlines[0].SubjectCode); // nearer deadline (2 days) first
            Assert.Equal("SOON", result.UpcomingDeadlines[1].SubjectCode);
            // Quick-link should point at the most urgent subject's next assignment.
            Assert.Equal(result.UpcomingDeadlines[0].NextAssignmentId, result.NextAssignmentId);
        }

        [Fact]
        public async Task GetMyProgressAsync_WhenGradingDeadlineSet_UsesItInsteadOfExamEndDate()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var gradingDeadline = new DateOnly(2026, 7, 27);
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectExamInfoClientDto(
                    subjectId, "PRN232", Guid.NewGuid(), "Final Exam",
                    ExamEndDate: new DateOnly(2026, 12, 31), GradingDeadline: gradingDeadline));
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Equal(gradingDeadline, Assert.Single(result.UpcomingDeadlines).Deadline);
        }

        [Fact]
        public async Task GetMyProgressAsync_WhenGradingDeadlineNotSet_FallsBackToExamEndDate()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var examEndDate = new DateOnly(2026, 12, 31);
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetExamInfoAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectExamInfoClientDto(subjectId, "PRN232", Guid.NewGuid(), "Final Exam", examEndDate));
            var service = NewService(db, catalogClient);

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Equal(examEndDate, Assert.Single(result.UpcomingDeadlines).Deadline);
        }

        [Fact]
        public async Task GetMyProgressAsync_WhenNothingRemaining_NextAssignmentIdIsNull()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, DateTime.UtcNow));
            await db.SaveChangesAsync();

            var service = NewService(db, Substitute.For<IExamCatalogServiceClient>());

            var result = await service.GetMyProgressAsync(teacherId, CancellationToken.None);

            Assert.Null(result.NextAssignmentId);
            Assert.Equal(0, result.RemainingCount);
        }
    }
}

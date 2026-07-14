using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public class GradingProgressDashboardTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingSessionService NewService(GradingDbContext db) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            Substitute.For<ISubmissionServiceClient>(),
            Substitute.For<IExamCatalogServiceClient>(),
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());

        private static GradingAssignment MakeAssignment(
            Guid teacherId, Guid subjectId, GradingProgressStatus status,
            decimal? score = null, DateTime? submittedAt = null)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = status
            };

            if (score.HasValue || submittedAt.HasValue)
            {
                assignment.GradingForm = new GradingForm
                {
                    GradingAssignmentId = assignment.Id,
                    TotalScore = score ?? 0,
                    SubmittedAt = submittedAt
                };
            }

            return assignment;
        }

        [Fact]
        public async Task GetProgressDashboardAsync_ComputesCompletionPercentAndScoresPerSubject()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, score: 6m, submittedAt: DateTime.UtcNow),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Drafting),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(4, subject.TotalPapers);
            Assert.Equal(2, subject.CompletedPapers);
            Assert.Equal(1, subject.DraftingPapers);
            Assert.Equal(1, subject.NotStartedPapers);
            Assert.Equal(50m, subject.CompletionPercent);
            Assert.Equal(7m, subject.ScoreAvg);
            Assert.Equal(6m, subject.ScoreMin);
            Assert.Equal(8m, subject.ScoreMax);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_GroupsPerLecturerWithinSubject()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherA = Guid.NewGuid();
            var teacherB = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherA, subjectId, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow),
                MakeAssignment(teacherA, subjectId, GradingProgressStatus.Submitted, score: 9m, submittedAt: DateTime.UtcNow),
                MakeAssignment(teacherB, subjectId, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(2, subject.Lecturers.Count);
            var lecturerA = subject.Lecturers.Single(l => l.TeacherId == teacherA);
            var lecturerB = subject.Lecturers.Single(l => l.TeacherId == teacherB);
            Assert.Equal(2, lecturerA.AssignedCount);
            Assert.Equal(2, lecturerA.CompletedCount);
            Assert.Equal(1, lecturerB.AssignedCount);
            Assert.Equal(0, lecturerB.CompletedCount);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_ThroughputOnlyCountsSubmissionsWithinLast24Hours()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow.AddHours(-1)),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow.AddDays(-3)), // outside window
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(null, CancellationToken.None);

            var lecturer = Assert.Single(Assert.Single(result.Subjects).Lecturers);
            // Only 1 of the 2 submissions falls inside the 24h window => throughput = 1/24.
            Assert.Equal(Math.Round(1 / 24m, 2), lecturer.ThroughputPerHour);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_WhenNoRecentThroughput_EtaIsNull()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.NotStarted),
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Drafting));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Null(subject.ThroughputPerHour);
            Assert.Null(subject.EstimatedFinish);
            Assert.Null(subject.Lecturers.Single().EstimatedFinish);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_WhenSubjectFullyCompleted_EtaIsNullEvenWithThroughput()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            db.GradingAssignments.Add(
                MakeAssignment(teacherId, subjectId, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(100m, subject.CompletionPercent);
            Assert.Null(subject.EstimatedFinish); // nothing left to grade — no ETA needed
        }

        [Fact]
        public async Task GetProgressDashboardAsync_FiltersBySubjectIds()
        {
            using var db = NewInMemoryContext();
            var wantedSubject = Guid.NewGuid();
            var otherSubject = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(teacherId, wantedSubject, GradingProgressStatus.Submitted, score: 8m, submittedAt: DateTime.UtcNow),
                MakeAssignment(teacherId, otherSubject, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetProgressDashboardAsync(new[] { wantedSubject }, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(wantedSubject, subject.SubjectId);
        }
    }
}

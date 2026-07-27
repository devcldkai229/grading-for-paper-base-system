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
    public class GradingTimeTrackingTests
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

        private static GradingAssignment SeedAssignment(
            GradingDbContext db, Guid teacherId, GradingProgressStatus status = GradingProgressStatus.NotStarted,
            int activeSecondsSpent = 0, Guid? subjectId = null)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId ?? Guid.NewGuid(),
                TeacherId = teacherId,
                Status = status
            };
            assignment.GradingForm = new GradingForm
            {
                GradingAssignmentId = assignment.Id,
                TotalScore = 0,
                ActiveSecondsSpent = activeSecondsSpent
            };
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        [Fact]
        public async Task RecordHeartbeatAsync_AccumulatesSecondsAcrossMultipleCalls()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId);

            var first = await service.RecordHeartbeatAsync(assignment.Id, teacherId, 15, CancellationToken.None);
            var second = await service.RecordHeartbeatAsync(assignment.Id, teacherId, 15, CancellationToken.None);

            Assert.True(first);
            Assert.True(second);
            var form = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(30, form.ActiveSecondsSpent);
            Assert.NotNull(form.LastActiveAt);
        }

        [Fact]
        public async Task RecordHeartbeatAsync_ClampsSecondsAboveMax()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId);

            await service.RecordHeartbeatAsync(assignment.Id, teacherId, 500, CancellationToken.None);

            var form = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(60, form.ActiveSecondsSpent);
        }

        [Fact]
        public async Task RecordHeartbeatAsync_ClampsNegativeSecondsToZero()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId);

            var recorded = await service.RecordHeartbeatAsync(assignment.Id, teacherId, -10, CancellationToken.None);

            Assert.True(recorded);
            var form = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(0, form.ActiveSecondsSpent);
        }

        [Fact]
        public async Task RecordHeartbeatAsync_WhenWrongTeacher_ReturnsFalseAndDoesNotMutate()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var ownerTeacherId = Guid.NewGuid();
            var otherTeacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, ownerTeacherId, activeSecondsSpent: 10);

            var recorded = await service.RecordHeartbeatAsync(assignment.Id, otherTeacherId, 15, CancellationToken.None);

            Assert.False(recorded);
            var form = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(10, form.ActiveSecondsSpent);
        }

        [Fact]
        public async Task RecordHeartbeatAsync_WhenAssignmentNotFound_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var recorded = await service.RecordHeartbeatAsync(Guid.NewGuid(), Guid.NewGuid(), 15, CancellationToken.None);

            Assert.False(recorded);
        }

        [Fact]
        public async Task RecordHeartbeatAsync_WhenAlreadySubmitted_ReturnsFalseAndDoesNotMutate()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId, GradingProgressStatus.Submitted, activeSecondsSpent: 120);

            var recorded = await service.RecordHeartbeatAsync(assignment.Id, teacherId, 15, CancellationToken.None);

            Assert.False(recorded);
            var form = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(120, form.ActiveSecondsSpent);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_ComputesAvgGradingMinutesPerPaper_OnlyOverSubmittedRows()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            SeedAssignment(db, teacherId, GradingProgressStatus.Submitted, activeSecondsSpent: 120, subjectId: subjectId); // 2 min
            SeedAssignment(db, teacherId, GradingProgressStatus.Submitted, activeSecondsSpent: 240, subjectId: subjectId); // 4 min
            SeedAssignment(db, teacherId, GradingProgressStatus.Drafting, activeSecondsSpent: 600, subjectId: subjectId); // in progress, excluded

            var result = await service.GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            var lecturer = Assert.Single(subject.Lecturers);
            Assert.Equal(3m, subject.AvgGradingMinutesPerPaper); // (2+4)/2 = 3 minutes, drafting excluded
            Assert.Equal(3m, lecturer.AvgGradingMinutesPerPaper);
        }

        [Fact]
        public async Task GetProgressDashboardAsync_WhenNoSubmittedPapers_AvgGradingMinutesPerPaperIsNull()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            SeedAssignment(db, teacherId, GradingProgressStatus.Drafting, activeSecondsSpent: 300);

            var result = await service.GetProgressDashboardAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            var lecturer = Assert.Single(subject.Lecturers);
            Assert.Null(subject.AvgGradingMinutesPerPaper);
            Assert.Null(lecturer.AvgGradingMinutesPerPaper);
        }
    }
}

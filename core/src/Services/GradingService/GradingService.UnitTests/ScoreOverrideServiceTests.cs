using System;
using System.Collections.Generic;
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
    public class ScoreOverrideServiceTests
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
            new MiniExcelGradeExportFileBuilder());

        private static GradingAssignment SeedSubmittedAssignment(GradingDbContext db, Guid teacherId, decimal score = 5m)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = Guid.NewGuid(),
                TeacherId = teacherId,
                Status = GradingProgressStatus.Submitted
            };

            var form = new GradingForm
            {
                GradingAssignmentId = assignment.Id,
                TotalScore = score,
                RowVersion = 1,
                SubmittedAt = DateTime.UtcNow
            };
            form.QuestionGradeDetails.Add(new QuestionGradeDetail
            {
                GradingFormId = form.Id,
                QuestionNumber = "Q1",
                OrderIndex = 0,
                Score = score,
                MaxScore = 10m
            });

            assignment.GradingForm = form;
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static OverrideMarksRequest MakeRequest(decimal score, string reason) => new(
            new List<QuestionMarkInput> { new("Q1", score, "corrected") },
            "updated paper comment", "internal note", reason);

        [Fact]
        public async Task OverrideMarksAsync_AsAdmin_CanOverrideAnotherTeachersSubmittedAssignment()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId, score: 5m);

            var (result, notFound, error) = await service.OverrideMarksAsync(
                assignment.Id, adminId, isAdmin: true, MakeRequest(9m, "student appealed"), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(9m, result!.TotalScore);

            var updatedForm = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(9m, updatedForm.TotalScore);
            Assert.Equal(2, updatedForm.RowVersion);
        }

        [Fact]
        public async Task OverrideMarksAsync_AsLecturer_CannotOverrideAnotherTeachersAssignment()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var ownerTeacherId = Guid.NewGuid();
            var otherLecturerId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, ownerTeacherId);

            var (result, notFound, error) = await service.OverrideMarksAsync(
                assignment.Id, otherLecturerId, isAdmin: false, MakeRequest(9m, "trying to fix someone else's paper"), CancellationToken.None);

            Assert.True(notFound);
            Assert.Null(result);
            Assert.Null(error);

            var untouchedForm = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(5m, untouchedForm.TotalScore); // unchanged
        }

        [Fact]
        public async Task OverrideMarksAsync_AsLecturer_CanOverrideOwnSubmittedAssignment()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId);

            var (result, notFound, error) = await service.OverrideMarksAsync(
                assignment.Id, teacherId, isAdmin: false, MakeRequest(7m, "re-checked my own grading"), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(error);
            Assert.Equal(7m, result!.TotalScore);
        }

        [Fact]
        public async Task OverrideMarksAsync_WithBlankReason_ReturnsErrorAndDoesNotWriteAuditLog()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId);

            var (result, notFound, error) = await service.OverrideMarksAsync(
                assignment.Id, teacherId, isAdmin: false, MakeRequest(7m, "  "), CancellationToken.None);

            Assert.Null(result);
            Assert.False(notFound);
            Assert.NotNull(error);
            Assert.Empty(db.AuditLogs);
        }

        [Fact]
        public async Task OverrideMarksAsync_WithOutOfRangeScore_ReturnsErrorAndDoesNotMutateForm()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId, score: 5m);

            var (result, notFound, error) = await service.OverrideMarksAsync(
                assignment.Id, teacherId, isAdmin: false, MakeRequest(999m, "bad input"), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            var untouchedForm = await db.GradingForms.AsNoTracking()
                .FirstAsync(f => f.GradingAssignmentId == assignment.Id);
            Assert.Equal(5m, untouchedForm.TotalScore);
        }

        [Fact]
        public async Task OverrideMarksAsync_WritesFullAuditEntryWithReasonAndOldNewSnapshots()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId, score: 5m);

            await service.OverrideMarksAsync(
                assignment.Id, adminId, isAdmin: true, MakeRequest(9m, "student appealed to the dean"), CancellationToken.None);

            var log = await db.AuditLogs.AsNoTracking().SingleAsync();
            Assert.Equal(adminId, log.UserId);
            Assert.Equal("ScoreOverridden", log.Action);
            Assert.Equal("student appealed to the dean", log.Reason);
            Assert.Contains("5", log.OldValue);
            Assert.Contains("9", log.NewValue);
        }

        [Fact]
        public async Task GetAuditTrailAsync_AsLecturer_CannotReadAnotherTeachersHistory()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var ownerTeacherId = Guid.NewGuid();
            var otherLecturerId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, ownerTeacherId);
            db.AuditLogs.Add(new AuditLog
            {
                UserId = ownerTeacherId,
                Action = "ScoreUpdated",
                EntityType = "GradingForm",
                EntityId = assignment.GradingForm!.Id
            });
            await db.SaveChangesAsync();

            var (result, notFound) = await service.GetAuditTrailAsync(
                assignment.Id, otherLecturerId, isAdmin: false, CancellationToken.None);

            Assert.True(notFound);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAuditTrailAsync_ReturnsEntriesNewestFirst()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var teacherId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId);
            var formId = assignment.GradingForm!.Id;

            var oldest = new AuditLog { UserId = teacherId, Action = "ScoreUpdated", EntityType = "GradingForm", EntityId = formId };
            var middle = new AuditLog { UserId = teacherId, Action = "FormSubmitted", EntityType = "GradingForm", EntityId = formId };
            var newest = new AuditLog { UserId = teacherId, Action = "ScoreOverridden", EntityType = "GradingForm", EntityId = formId, Reason = "fix" };
            db.AuditLogs.AddRange(oldest, middle, newest);

            // CreatedAt has a protected setter (set once in the Entity base ctor); bypass via the
            // change tracker so this test can control ordering without relying on wall-clock ticks.
            db.Entry(oldest).Property(nameof(AuditLog.CreatedAt)).CurrentValue = DateTime.UtcNow.AddMinutes(-10);
            db.Entry(middle).Property(nameof(AuditLog.CreatedAt)).CurrentValue = DateTime.UtcNow.AddMinutes(-5);
            db.Entry(newest).Property(nameof(AuditLog.CreatedAt)).CurrentValue = DateTime.UtcNow;

            await db.SaveChangesAsync();

            var (result, notFound) = await service.GetAuditTrailAsync(
                assignment.Id, teacherId, isAdmin: false, CancellationToken.None);

            Assert.False(notFound);
            Assert.Equal(3, result!.Count);
            Assert.Equal("ScoreOverridden", result[0].Action);
            Assert.Equal("fix", result[0].Reason);
            Assert.Equal("FormSubmitted", result[1].Action);
            Assert.Equal("ScoreUpdated", result[2].Action);
        }
    }
}

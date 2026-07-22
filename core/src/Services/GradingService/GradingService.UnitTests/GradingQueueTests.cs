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
    public class GradingQueueTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
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
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());

        private static GradingAssignment SeedAssignment(
            GradingDbContext db, Guid teacherId, Guid subjectId, GradingProgressStatus status,
            bool isFlagged = false, Guid? studentPaperId = null)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = studentPaperId ?? Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = status,
                IsFlagged = isFlagged
            };
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static ISubmissionServiceClient StubPaperSummaries(
            params (Guid PaperId, string? Alias, int? AliasNumber)[] papers)
        {
            var client = Substitute.For<ISubmissionServiceClient>();
            client.GetPaperSummariesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(papers
                    .Select(p => new InternalPaperSummaryClientDto(
                        p.PaperId, Guid.NewGuid(), Guid.NewGuid(), p.Alias, p.AliasNumber, Guid.NewGuid()))
                    .ToList());
            return client;
        }

        // --- Repository-level: ListQueueRowsAsync ---

        [Fact]
        public async Task ListQueueRowsAsync_FiltersByTeacher()
        {
            using var db = NewInMemoryContext();
            var teacherA = Guid.NewGuid();
            var teacherB = Guid.NewGuid();
            SeedAssignment(db, teacherA, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            SeedAssignment(db, teacherB, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var repo = new GradingAssignmentRepository(db);

            var rows = await repo.ListQueueRowsAsync(teacherA, null, null, CancellationToken.None);

            Assert.Single(rows);
        }

        [Fact]
        public async Task ListQueueRowsAsync_FiltersByStatus()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.Drafting);
            var repo = new GradingAssignmentRepository(db);

            var rows = await repo.ListQueueRowsAsync(teacherId, GradingProgressStatus.Drafting, null, CancellationToken.None);

            Assert.Single(rows);
            Assert.Equal(GradingProgressStatus.Drafting, rows[0].Status);
        }

        [Fact]
        public async Task ListQueueRowsAsync_FiltersByFlagged()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted, isFlagged: true);
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted, isFlagged: false);
            var repo = new GradingAssignmentRepository(db);

            var rows = await repo.ListQueueRowsAsync(teacherId, null, flaggedOnly: true, CancellationToken.None);

            Assert.Single(rows);
            Assert.True(rows[0].IsFlagged);
        }

        [Fact]
        public async Task ListQueueRowsAsync_CombinesStatusAndFlagged()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.Submitted, isFlagged: true);
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.Submitted, isFlagged: false);
            SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted, isFlagged: true);
            var repo = new GradingAssignmentRepository(db);

            var rows = await repo.ListQueueRowsAsync(
                teacherId, GradingProgressStatus.Submitted, flaggedOnly: true, CancellationToken.None);

            Assert.Single(rows);
        }

        // --- Service-level: GetGradingQueueAsync ---

        [Fact]
        public async Task GetGradingQueueAsync_WhenNoRows_SkipsSubmissionServiceCall()
        {
            using var db = NewInMemoryContext();
            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            var service = NewService(db, submissionClient);

            var result = await service.GetGradingQueueAsync(
                Guid.NewGuid(), null, null, null, null, 1, 20, CancellationToken.None);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            await submissionClient.DidNotReceive().GetPaperSummariesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetGradingQueueAsync_FiltersByAliasSubstring()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var a = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var b = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var client = StubPaperSummaries(
                (a.StudentPaperId, "Student_0001", 1),
                (b.StudentPaperId, "Student_0002", 2));
            var service = NewService(db, client);

            var result = await service.GetGradingQueueAsync(
                teacherId, null, null, "0001", null, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal("Student_0001", result.Items[0].StudentAlias);
        }

        [Fact]
        public async Task GetGradingQueueAsync_MatchesExactAliasNumber()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var a = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var b = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var client = StubPaperSummaries(
                (a.StudentPaperId, "Student_0012", 12),
                (b.StudentPaperId, "Student_0999", 999));
            var service = NewService(db, client);

            var result = await service.GetGradingQueueAsync(
                teacherId, null, null, "12", null, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(12, result.Items[0].AliasNumber);
        }

        [Fact]
        public async Task GetGradingQueueAsync_UnresolvedAliasStillAppearsWithNullAlias()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var a = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var client = Substitute.For<ISubmissionServiceClient>();
            client.GetPaperSummariesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<InternalPaperSummaryClientDto>?)null);
            var service = NewService(db, client);

            var result = await service.GetGradingQueueAsync(
                teacherId, null, null, null, null, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Null(result.Items[0].StudentAlias);
        }

        [Fact]
        public async Task GetGradingQueueAsync_OrdersByAliasNumberAscending()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var a = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var b = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var client = StubPaperSummaries(
                (a.StudentPaperId, "Student_0005", 5),
                (b.StudentPaperId, "Student_0002", 2));
            var service = NewService(db, client);

            var result = await service.GetGradingQueueAsync(
                teacherId, null, null, null, null, 1, 20, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(2, result.Items[0].AliasNumber);
            Assert.Equal(5, result.Items[1].AliasNumber);
        }

        [Fact]
        public async Task GetGradingQueueAsync_PaginatesResults()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var papers = new List<(Guid, string?, int?)>();
            for (var i = 1; i <= 5; i++)
            {
                var assignment = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
                papers.Add((assignment.StudentPaperId, $"Student_{i:D4}", i));
            }
            var client = StubPaperSummaries(papers.ToArray());
            var service = NewService(db, client);

            var page1 = await service.GetGradingQueueAsync(teacherId, null, null, null, null, 1, 2, CancellationToken.None);
            var page2 = await service.GetGradingQueueAsync(teacherId, null, null, null, null, 2, 2, CancellationToken.None);

            Assert.Equal(5, page1.TotalCount);
            Assert.Equal(3, page1.TotalPages);
            Assert.Equal(2, page1.Items.Count);
            Assert.Equal(1, page1.Items[0].AliasNumber);
            Assert.Equal(2, page1.Items[1].AliasNumber);
            Assert.Equal(2, page2.Items.Count);
            Assert.Equal(3, page2.Items[0].AliasNumber);
        }

        // --- Service-level: SetFlagAsync ---

        [Fact]
        public async Task SetFlagAsync_SetsFlagToTrue()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var service = NewService(db);

            var updated = await service.SetFlagAsync(assignment.Id, teacherId, true, CancellationToken.None);

            Assert.True(updated);
            var reloaded = await db.GradingAssignments.FindAsync(assignment.Id);
            Assert.True(reloaded!.IsFlagged);
        }

        [Fact]
        public async Task SetFlagAsync_ClearsFlag()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted, isFlagged: true);
            var service = NewService(db);

            var updated = await service.SetFlagAsync(assignment.Id, teacherId, false, CancellationToken.None);

            Assert.True(updated);
            var reloaded = await db.GradingAssignments.FindAsync(assignment.Id);
            Assert.False(reloaded!.IsFlagged);
        }

        [Fact]
        public async Task SetFlagAsync_WrongTeacher_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var teacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, teacherId, Guid.NewGuid(), GradingProgressStatus.NotStarted);
            var service = NewService(db);

            var updated = await service.SetFlagAsync(assignment.Id, Guid.NewGuid(), true, CancellationToken.None);

            Assert.False(updated);
        }

        [Fact]
        public async Task SetFlagAsync_NonexistentAssignment_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var updated = await service.SetFlagAsync(Guid.NewGuid(), Guid.NewGuid(), true, CancellationToken.None);

            Assert.False(updated);
        }
    }
}

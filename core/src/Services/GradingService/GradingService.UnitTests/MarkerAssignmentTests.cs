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
    public class MarkerAssignmentTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingSessionService NewService(
            GradingDbContext db,
            ISubmissionServiceClient? submissionClient = null,
            IExamCatalogServiceClient? catalogClient = null) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            submissionClient ?? StubSubmission(),
            catalogClient ?? StubCatalog(),
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());

        private static IExamCatalogServiceClient StubCatalog()
        {
            var catalog = Substitute.For<IExamCatalogServiceClient>();
            catalog.GetGradingGridAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new SubjectGradingGridClientDto(
                    Guid.NewGuid(), 10m, 1,
                    new List<SubjectQuestionClientDto> { new("1", null, "Q1", 10m, 0) },
                    "Open"));
            return catalog;
        }

        private static ISubmissionServiceClient StubSubmission(int totalPapers = 100, int? maxAliasNumber = 100)
        {
            var client = Substitute.For<ISubmissionServiceClient>();
            client.GetSubjectPaperStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new SubjectPaperStatsClientDto(totalPapers, maxAliasNumber));
            client.ListPapersByAliasRangeAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<InternalPaperSummaryClientDto>());
            client.MarkPapersAssignedAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(0);
            return client;
        }

        private static ISubmissionServiceClient StubStats(int totalPapers, int? maxAliasNumber) =>
            StubSubmission(totalPapers, maxAliasNumber);

        private static MarkerAssignment SeedAssignment(
            GradingDbContext db, Guid subjectId, Guid teacherId, int aliasStart, int aliasEnd)
        {
            var assignment = new MarkerAssignment
            {
                SubjectId = subjectId,
                TeacherId = teacherId,
                AliasStart = aliasStart,
                AliasEnd = aliasEnd,
                AssignedBy = Guid.NewGuid()
            };
            db.MarkerAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static GradingAssignment SeedGradingAssignment(
            GradingDbContext db, Guid subjectId, Guid teacherId, Guid paperId, GradingProgressStatus status)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = paperId,
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = status
            };
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static ISubmissionServiceClient StubPapersInRange(
            Guid subjectId, int aliasStart, int aliasEnd, params (Guid PaperId, int Alias)[] papers)
        {
            var client = StubSubmission();
            var summaries = papers
                .Select(p => new InternalPaperSummaryClientDto(
                    p.PaperId, Guid.NewGuid(), subjectId, $"Student_{p.Alias:D4}", p.Alias, Guid.NewGuid()))
                .ToList();
            client.ListPapersByAliasRangeAsync(subjectId, aliasStart, aliasEnd, Arg.Any<CancellationToken>())
                .Returns(summaries);
            return client;
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithExplicitRange_CreatesAssignment()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(teacherId, 1, 20, null), adminId, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(1, result!.Assignment.AliasStart);
            Assert.Equal(20, result.Assignment.AliasEnd);
            Assert.Equal(teacherId, result.Assignment.TeacherId);
            Assert.Equal(adminId, result.Assignment.AssignedBy);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithQuota_AutoPicksNextContiguousRange()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 15),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(21, result!.Assignment.AliasStart);
            Assert.Equal(35, result.Assignment.AliasEnd);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithQuota_AndNoExistingAssignments_StartsAtOne()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 10),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.Equal(1, result!.Assignment.AliasStart);
            Assert.Equal(10, result.Assignment.AliasEnd);
        }

        [Theory]
        [InlineData(1, 20)]   // exact duplicate
        [InlineData(5, 15)]   // fully nested inside existing
        [InlineData(15, 25)]  // partial overlap at the tail
        [InlineData(0, 1)]    // partial overlap at the head (and invalid start, but overlap dominates)
        public async Task CreateMarkerAssignmentAsync_WithOverlappingRange_ReturnsErrorAndDoesNotCreate(
            int aliasStart, int aliasEnd)
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), aliasStart, aliasEnd, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Single(db.MarkerAssignments);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithAdjacentNonOverlappingRange_Succeeds()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 21, 40, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_DoesNotOverlapAcrossDifferentSubjects()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            SeedAssignment(db, Guid.NewGuid(), Guid.NewGuid(), 1, 20);
            var otherSubjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                otherSubjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithNeitherRangeNorQuota_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                Guid.NewGuid(), new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenSubjectHasNoSubmittedPapers_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 0, maxAliasNumber: null));
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Empty(db.MarkerAssignments);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenRangeExceedsHighestSubmittedAlias_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 30, maxAliasNumber: 30));
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 40, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Empty(db.MarkerAssignments);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithQuota_WhenComputedRangeExceedsSubmittedPapers_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 20, maxAliasNumber: 20));
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 15);

            // Only aliases 16-20 remain (5 papers), but quota asks for 10.
            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 10),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenRangeWithinSubmittedPapers_Succeeds()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 30, maxAliasNumber: 30));
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 30, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_MaterializesGradingAssignmentsForPapersInRange()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var paperId = Guid.NewGuid();

            var submission = StubSubmission(5, 5);
            submission.ListPapersByAliasRangeAsync(Arg.Any<Guid>(), 1, 3, Arg.Any<CancellationToken>())
                .Returns(new List<InternalPaperSummaryClientDto>
                {
                    new(paperId, Guid.NewGuid(), subjectId, "Student_0001", 1, Guid.NewGuid()),
                    new(Guid.NewGuid(), Guid.NewGuid(), subjectId, "Student_0002", 2, Guid.NewGuid()),
                    new(Guid.NewGuid(), Guid.NewGuid(), subjectId, "Student_0003", 3, Guid.NewGuid()),
                });

            var service = NewService(db, submission);
            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(teacherId, 1, 3, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(3, result!.MaterializedCount);
            Assert.Equal(3, await db.GradingAssignments.CountAsync());
            Assert.All(await db.GradingAssignments.ToListAsync(), a => Assert.Equal(teacherId, a.TeacherId));
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenSubmissionServiceUnreachable_SkipsValidationAndSucceeds()
        {
            using var db = NewInMemoryContext();
            var unreachableClient = Substitute.For<ISubmissionServiceClient>();
            unreachableClient.GetSubjectPaperStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((SubjectPaperStatsClientDto?)null);
            unreachableClient.ListPapersByAliasRangeAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<InternalPaperSummaryClientDto>?)null);
            var service = NewService(db, unreachableClient);
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 1000, null),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_WhenRangeExceedsHighestSubmittedAlias_ReturnsErrorAndDoesNotMutate()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 20, maxAliasNumber: 20));
            var subjectId = Guid.NewGuid();
            var assignment = SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                assignment.Id, new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 50),
                Guid.NewGuid(), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(result);
            Assert.NotNull(error);

            var untouched = await db.MarkerAssignments.AsNoTracking().SingleAsync(m => m.Id == assignment.Id);
            Assert.Equal(20, untouched.AliasEnd);
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_ChangesTeacherAndRange()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            var newTeacherId = Guid.NewGuid();
            var assignment = SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                assignment.Id, new ReassignMarkerAssignmentRequest(newTeacherId, 1, 25),
                Guid.NewGuid(), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(newTeacherId, result!.Assignment.TeacherId);
            Assert.Equal(25, result.Assignment.AliasEnd);
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_WithOverlapAgainstAnotherAssignment_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            var toReassign = SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);
            SeedAssignment(db, subjectId, Guid.NewGuid(), 21, 40);

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                toReassign.Id, new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 25),
                Guid.NewGuid(), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(result);
            Assert.NotNull(error);

            var untouched = await db.MarkerAssignments.AsNoTracking().SingleAsync(m => m.Id == toReassign.Id);
            Assert.Equal(20, untouched.AliasEnd); // unchanged
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_WhenNotFound_ReturnsNotFound()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                Guid.NewGuid(), new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 10),
                Guid.NewGuid(), CancellationToken.None);

            Assert.True(notFound);
            Assert.Null(result);
            Assert.Null(error);
        }

        [Fact]
        public async Task DeleteMarkerAssignmentAsync_RemovesAssignment_FreeingItsRangeForReuse()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            var assignment = SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (deleted, error) = await service.DeleteMarkerAssignmentAsync(assignment.Id, CancellationToken.None);
            Assert.True(deleted);
            Assert.Null(error);
            Assert.Empty(db.MarkerAssignments);

            // The freed range 1-20 must now be allocatable again without an overlap error.
            var (result, createError) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null),
                Guid.NewGuid(), CancellationToken.None);
            Assert.Null(createError);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DeleteMarkerAssignmentAsync_WhenNotFound_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var (deleted, error) = await service.DeleteMarkerAssignmentAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.False(deleted);
            Assert.Null(error);
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_WhenGradingInProgress_ReturnsErrorAndDoesNotMutate()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var paperId = Guid.NewGuid();
            var marker = SeedAssignment(db, subjectId, teacherId, 1, 5);
            SeedGradingAssignment(db, subjectId, teacherId, paperId, GradingProgressStatus.Drafting);
            var service = NewService(db, StubPapersInRange(subjectId, 1, 5, (paperId, 1)));

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                marker.Id, new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 5),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.False(notFound);
            Assert.Contains("bắt đầu chấm", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(teacherId, (await db.MarkerAssignments.FindAsync(marker.Id))!.TeacherId);
        }

        [Fact]
        public async Task DeleteMarkerAssignmentAsync_WhenGradingSubmitted_ReturnsErrorAndDoesNotDelete()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var paperId = Guid.NewGuid();
            var marker = SeedAssignment(db, subjectId, teacherId, 1, 5);
            SeedGradingAssignment(db, subjectId, teacherId, paperId, GradingProgressStatus.Submitted);
            var service = NewService(db, StubPapersInRange(subjectId, 1, 5, (paperId, 2)));

            var (deleted, error) = await service.DeleteMarkerAssignmentAsync(marker.Id, CancellationToken.None);

            Assert.False(deleted);
            Assert.Contains("bắt đầu chấm", error, StringComparison.OrdinalIgnoreCase);
            Assert.Single(db.MarkerAssignments);
        }

        [Fact]
        public async Task ListMarkerAssignmentsAsync_ReturnsOnlyThatSubjectOrderedByAliasStart()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 21, 40);
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);
            SeedAssignment(db, Guid.NewGuid(), Guid.NewGuid(), 1, 20); // other subject

            var result = await service.ListMarkerAssignmentsAsync(subjectId, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].AliasStart);
            Assert.Equal(21, result[1].AliasStart);
        }

        [Fact]
        public async Task CreateFolderAssignmentAsync_MaterializesPapersInBatch()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var paperIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            var submission = StubSubmission();
            submission.GetBatchPapersAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(new BatchPapersClientDto(
                    batchId, subjectId, Guid.NewGuid(),
                    paperIds.Select((id, i) => new BatchPaperClientDto(id, subjectId, i + 1)).ToList()));
            submission.MarkPapersAssignedAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(3);

            var service = NewService(db, submission);
            var (result, error) = await service.CreateFolderAssignmentAsync(
                subjectId,
                new CreateFolderAssignmentRequest(teacherId, batchId, "papers.zip"),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(3, result!.MaterializedCount);
            Assert.Equal(batchId, result.Assignment.BatchId);
            Assert.Equal("papers.zip", result.Assignment.ZipFileName);
            Assert.Equal(3, db.GradingAssignments.Count(a => a.TeacherId == teacherId));
        }

        [Fact]
        public async Task CreateFolderAssignmentAsync_RejectsDuplicateBatch()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            db.MarkerAssignments.Add(new MarkerAssignment
            {
                SubjectId = subjectId,
                TeacherId = Guid.NewGuid(),
                AliasStart = 1,
                AliasEnd = 3,
                BatchId = batchId,
                AssignedBy = Guid.NewGuid()
            });
            await db.SaveChangesAsync();

            var submission = StubSubmission();
            submission.GetBatchPapersAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(new BatchPapersClientDto(
                    batchId, subjectId, Guid.NewGuid(),
                    new List<BatchPaperClientDto> { new(Guid.NewGuid(), subjectId, 1) }));

            var service = NewService(db, submission);
            var (result, error) = await service.CreateFolderAssignmentAsync(
                subjectId,
                new CreateFolderAssignmentRequest(Guid.NewGuid(), batchId, "dup.zip"),
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.Null(result);
            Assert.Contains("đã được phân công", error, StringComparison.OrdinalIgnoreCase);
        }
    }
}

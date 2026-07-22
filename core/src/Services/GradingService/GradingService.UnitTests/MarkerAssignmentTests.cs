using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Application.Services;
using GradingService.Domain.Entities;
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
            GradingDbContext db, ISubmissionServiceClient? submissionClient = null)
        {
            submissionClient ??= StubStats(1000, 1000);
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetGradingGridAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => new SubjectGradingGridClientDto(
                    call.ArgAt<Guid>(0), 10m, 1,
                    new[] { new SubjectQuestionClientDto("Q1", null, "Question 1", 10m, 0) },
                    "Open"));

            return new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            submissionClient,
            catalogClient,
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());
        }

        private static ISubmissionServiceClient StubStats(int totalPapers, int? maxAliasNumber)
        {
            var client = Substitute.For<ISubmissionServiceClient>();
            client.GetSubjectPaperStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new SubjectPaperStatsClientDto(totalPapers, maxAliasNumber));
            client.GetBatchPapersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var batchId = call.ArgAt<Guid>(0);
                    var papers = Enumerable.Range(1, maxAliasNumber ?? 0)
                        .Take(totalPapers)
                        .Select(alias => new BatchPaperClientDto(Guid.NewGuid(), batchId, alias))
                        .ToList();
                    return new BatchPapersClientDto(batchId, batchId, Guid.NewGuid(), papers);
                });
            client.SetPapersAssignmentStatusAsync(
                    Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(true);
            return client;
        }

        private static MarkerAssignment SeedAssignment(
            GradingDbContext db, Guid subjectId, Guid teacherId, int aliasStart, int aliasEnd)
        {
            var assignment = new MarkerAssignment
            {
                SubjectId = subjectId,
                BatchId = subjectId,
                TeacherId = teacherId,
                AliasStart = aliasStart,
                AliasEnd = aliasEnd,
                AssignedBy = Guid.NewGuid()
            };
            db.MarkerAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
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
                subjectId, new CreateMarkerAssignmentRequest(teacherId, 1, 20, null, subjectId), adminId, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(1, result!.AliasStart);
            Assert.Equal(20, result.AliasEnd);
            Assert.Equal(teacherId, result.TeacherId);
            Assert.Equal(adminId, result.AssignedBy);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithQuota_AutoPicksNextContiguousRange()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();
            SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 15, subjectId),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(21, result!.AliasStart);
            Assert.Equal(35, result.AliasEnd);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WithQuota_AndNoExistingAssignments_StartsAtOne()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 10, subjectId),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.Equal(1, result!.AliasStart);
            Assert.Equal(10, result.AliasEnd);
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), aliasStart, aliasEnd, null, subjectId),
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 21, 40, null, subjectId),
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
                otherSubjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null, otherSubjectId),
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null, subjectId),
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 40, null, subjectId),
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, 10, subjectId),
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
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 30, null, subjectId),
                Guid.NewGuid(), CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenSubmissionServiceUnreachable_FailsClosedForBatchSafety()
        {
            using var db = NewInMemoryContext();
            var unreachableClient = Substitute.For<ISubmissionServiceClient>();
            unreachableClient.GetSubjectPaperStatsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((SubjectPaperStatsClientDto?)null);
            var service = NewService(db, unreachableClient);
            var subjectId = Guid.NewGuid();

            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 1000, null, subjectId),
                Guid.NewGuid(), CancellationToken.None);

            Assert.NotNull(error);
            Assert.Null(result);
            Assert.Empty(db.MarkerAssignments);
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_WhenRangeExceedsHighestSubmittedAlias_ReturnsErrorAndDoesNotMutate()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db, StubStats(totalPapers: 20, maxAliasNumber: 20));
            var subjectId = Guid.NewGuid();
            var assignment = SeedAssignment(db, subjectId, Guid.NewGuid(), 1, 20);

            var (result, notFound, error) = await service.ReassignMarkerAssignmentAsync(
                assignment.Id, new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 50, subjectId),
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
                assignment.Id, new ReassignMarkerAssignmentRequest(newTeacherId, 1, 25, subjectId),
                Guid.NewGuid(), CancellationToken.None);

            Assert.False(notFound);
            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Equal(newTeacherId, result!.TeacherId);
            Assert.Equal(25, result.AliasEnd);
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
                toReassign.Id, new ReassignMarkerAssignmentRequest(Guid.NewGuid(), 1, 25, subjectId),
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

            var (deleted, deleteError) = await service.DeleteMarkerAssignmentAsync(assignment.Id, CancellationToken.None);
            Assert.True(deleted);
            Assert.Null(deleteError);
            Assert.Empty(db.MarkerAssignments);

            // The freed range 1-20 must now be allocatable again without an overlap error.
            var (result, error) = await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(Guid.NewGuid(), 1, 20, null, subjectId),
                Guid.NewGuid(), CancellationToken.None);
            Assert.Null(error);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task DeleteMarkerAssignmentAsync_WhenNotFound_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var service = NewService(db);

            var (deleted, deleteError) = await service.DeleteMarkerAssignmentAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.False(deleted);
            Assert.Null(deleteError);
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
    }
}

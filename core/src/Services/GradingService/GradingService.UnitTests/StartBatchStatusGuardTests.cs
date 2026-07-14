using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Application.Services;
using GradingService.Infrastructure.Files;
using GradingService.Infrastructure.Persistence;
using GradingService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace GradingService.UnitTests
{
    public class StartBatchStatusGuardTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingSessionService NewService(
            GradingDbContext db, ISubmissionServiceClient submissionClient, IExamCatalogServiceClient catalogClient) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            submissionClient,
            catalogClient,
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            Substitute.For<IMessagePublisher>());

        private static BatchPapersClientDto MakeBatch(Guid batchId, Guid subjectId, Guid uploadedBy) =>
            new(batchId, subjectId, uploadedBy, new List<BatchPaperClientDto>
            {
                new(Guid.NewGuid(), subjectId, 1),
            });

        private static SubjectGradingGridClientDto MakeGrid(Guid subjectId, string? status) =>
            new(subjectId, 10m, 1, new List<SubjectQuestionClientDto>
            {
                new("1", null, null, 10m, 0),
            }, status);

        [Fact]
        public async Task StartBatchAsync_WhenSubjectIsClosed_ReturnsErrorAndCreatesNoAssignments()
        {
            using var db = NewInMemoryContext();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            submissionClient.GetBatchPapersAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(MakeBatch(batchId, subjectId, teacherId));
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetGradingGridAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(MakeGrid(subjectId, "Closed"));

            var service = NewService(db, submissionClient, catalogClient);

            var (result, error) = await service.StartBatchAsync(batchId, teacherId, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            Assert.Contains("closed", error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(db.GradingAssignments);
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Open")]
        [InlineData("Grading")]
        public async Task StartBatchAsync_WhenSubjectIsNotClosed_Succeeds(string status)
        {
            using var db = NewInMemoryContext();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            submissionClient.GetBatchPapersAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(MakeBatch(batchId, subjectId, teacherId));
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.GetGradingGridAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(MakeGrid(subjectId, status));

            var service = NewService(db, submissionClient, catalogClient);

            var (result, error) = await service.StartBatchAsync(batchId, teacherId, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Single(db.GradingAssignments);
        }

        [Fact]
        public async Task StartBatchAsync_WhenBatchNotFound_ReturnsNullResultAndNullError()
        {
            using var db = NewInMemoryContext();
            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            submissionClient.GetBatchPapersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((BatchPapersClientDto?)null);
            var service = NewService(db, submissionClient, Substitute.For<IExamCatalogServiceClient>());

            var (result, error) = await service.StartBatchAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
            Assert.Null(error);
        }

        [Fact]
        public async Task StartBatchAsync_WhenCallerIsNotUploader_ReturnsNullResultAndNullError()
        {
            using var db = NewInMemoryContext();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var actualUploader = Guid.NewGuid();
            var otherTeacher = Guid.NewGuid();

            var submissionClient = Substitute.For<ISubmissionServiceClient>();
            submissionClient.GetBatchPapersAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(MakeBatch(batchId, subjectId, actualUploader));
            var service = NewService(db, submissionClient, Substitute.For<IExamCatalogServiceClient>());

            var (result, error) = await service.StartBatchAsync(batchId, otherTeacher, CancellationToken.None);

            Assert.Null(result);
            Assert.Null(error);
        }
    }
}

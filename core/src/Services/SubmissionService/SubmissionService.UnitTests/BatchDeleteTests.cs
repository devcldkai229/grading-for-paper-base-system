using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SubmissionService.Application;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Application.Services;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class BatchDeleteTests
    {
        private readonly IBatchRepository _batchRepository;
        private readonly IStudentPaperRepository _paperRepository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IS3Service _s3Service;
        private readonly ISubmissionAuditLogRepository _auditLogRepository;
        private readonly BatchService _service;

        public BatchDeleteTests()
        {
            _batchRepository = Substitute.For<IBatchRepository>();
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _messagePublisher = Substitute.For<IMessagePublisher>();
            _s3Service = Substitute.For<IS3Service>();
            _auditLogRepository = Substitute.For<ISubmissionAuditLogRepository>();

            _service = new BatchService(
                _batchRepository, _paperRepository, _s3Service, _messagePublisher, _auditLogRepository);
        }

        private static BatchDto MakeBatch(Guid id, Guid subjectId, Guid uploadedBy, string status, string? zipS3Key = null) =>
            new(id, subjectId, zipS3Key ?? $"submissions/{subjectId}/{id}.zip", "papers.zip", 2,
                status, uploadedBy, null, DateTime.UtcNow);

        private static PaperDeletionInfoDto MakePaper(Guid batchId, Guid subjectId, string status, params string[] s3Keys) =>
            new(Guid.NewGuid(), batchId, subjectId, Guid.NewGuid(), status,
                Array.ConvertAll(s3Keys, k => new PaperFileRefDto(Guid.NewGuid(), k)));

        [Fact]
        public async Task DeleteBatch_WhenBatchNotFound_ReturnsNotFound()
        {
            var batchId = Guid.NewGuid();
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns((BatchDto?)null);

            var result = await _service.DeleteBatchAsync(batchId, Guid.NewGuid(), isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.NotFound, result.Status);
            await _s3Service.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteBatch_WhenNotOwnerAndNotAdmin_ReturnsForbidden()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Ready");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

            var result = await _service.DeleteBatchAsync(batchId, Guid.NewGuid(), isAdmin: false, CancellationToken.None); // different user than owner

            Assert.Equal(OperationStatus.Forbidden, result.Status);
            await _paperRepository.DidNotReceive().GetPapersForDeletionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _batchRepository.DidNotReceive().DeleteBatchRecordAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteBatch_WhenStillExtracting_ReturnsConflictAndDoesNotDelete()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Extracting");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

            var result = await _service.DeleteBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            await _batchRepository.DidNotReceive().DeleteBatchRecordAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteBatch_WhenAnyPaperAlreadyStartedGrading_ReturnsConflictAndDoesNotDelete()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Ready");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

            var papers = new List<PaperDeletionInfoDto>
            {
                MakePaper(batchId, subjectId, "ReadyToAssign", "k1"),
                MakePaper(batchId, subjectId, "Assigned", "k2"), // grading already started
            };
            _paperRepository.GetPapersForDeletionAsync(batchId, Arg.Any<CancellationToken>()).Returns(papers);

            var result = await _service.DeleteBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            await _s3Service.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _paperRepository.DidNotReceive().DeletePapersByBatchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _batchRepository.DidNotReceive().DeleteBatchRecordAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteBatch_WhenAllPapersReadyToAssignAndOwner_PurgesS3AndDbAndAudits()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Ready", zipS3Key: "zip/key.zip");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

            var papers = new List<PaperDeletionInfoDto>
            {
                MakePaper(batchId, subjectId, "ReadyToAssign", "k1", "k2"),
                MakePaper(batchId, subjectId, "ReadyToAssign", "k3"),
            };
            _paperRepository.GetPapersForDeletionAsync(batchId, Arg.Any<CancellationToken>()).Returns(papers);

            var result = await _service.DeleteBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Success, result.Status);
            await _s3Service.Received(1).DeleteAsync("k1", Arg.Any<CancellationToken>());
            await _s3Service.Received(1).DeleteAsync("k2", Arg.Any<CancellationToken>());
            await _s3Service.Received(1).DeleteAsync("k3", Arg.Any<CancellationToken>());
            await _s3Service.Received(1).DeleteAsync("zip/key.zip", Arg.Any<CancellationToken>());
            await _paperRepository.Received(1).DeletePapersByBatchAsync(batchId, Arg.Any<CancellationToken>());
            await _batchRepository.Received(1).DeleteBatchRecordAsync(batchId, Arg.Any<CancellationToken>());
            await _auditLogRepository.Received(1).LogAsync(
                "DeleteBatch", "Batch", batchId, subjectId, ownerId,
                Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteBatch_WhenAdmin_CanDeleteBatchOwnedBySomeoneElse()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Ready");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            _paperRepository.GetPapersForDeletionAsync(batchId, Arg.Any<CancellationToken>())
                .Returns(new List<PaperDeletionInfoDto>());

            var result = await _service.DeleteBatchAsync(batchId, Guid.NewGuid(), isAdmin: true, CancellationToken.None); // not the owner, but Admin

            Assert.Equal(OperationStatus.Success, result.Status);
            await _batchRepository.Received(1).DeleteBatchRecordAsync(batchId, Arg.Any<CancellationToken>());
        }
    }
}

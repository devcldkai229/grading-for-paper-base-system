using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using SubmissionService.Application;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Application.Services;
using SubmissionService.Domain.Messages;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class BatchRetryTests
    {
        private readonly IBatchRepository _batchRepository;
        private readonly IStudentPaperRepository _paperRepository;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IS3Service _s3Service;
        private readonly ISubmissionAuditLogRepository _auditLogRepository;
        private readonly BatchService _service;

        public BatchRetryTests()
        {
            _batchRepository = Substitute.For<IBatchRepository>();
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _messagePublisher = Substitute.For<IMessagePublisher>();
            _s3Service = Substitute.For<IS3Service>();
            _auditLogRepository = Substitute.For<ISubmissionAuditLogRepository>();

            _service = new BatchService(
                _batchRepository, _paperRepository, _s3Service, _messagePublisher, _auditLogRepository);
        }

        private static BatchDto MakeBatch(Guid id, Guid subjectId, Guid uploadedBy, string status) =>
            new(id, subjectId, $"submissions/{subjectId}/{id}.zip", "papers.zip", 0,
                status, uploadedBy, status == "Failed" ? "boom" : null, DateTime.UtcNow);

        [Fact]
        public async Task RetryBatch_WhenBatchNotFound_ReturnsNotFound()
        {
            var batchId = Guid.NewGuid();
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns((BatchDto?)null);

            var result = await _service.RetryBatchAsync(batchId, Guid.NewGuid(), isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.NotFound, result.Status);
            await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_WhenNotOwnerAndNotAdmin_ReturnsForbidden()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Failed");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);

            var result = await _service.RetryBatchAsync(batchId, Guid.NewGuid(), isAdmin: false, CancellationToken.None); // different user than owner

            Assert.Equal(OperationStatus.Forbidden, result.Status);
            await _batchRepository.DidNotReceive().TryMarkFailedForRetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_WhenNotFailed_ReturnsConflictAndDoesNotPublish()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Extracting");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            // Simulate the atomic guard rejecting the transition because status isn't Failed.
            _batchRepository.TryMarkFailedForRetryAsync(batchId, Arg.Any<CancellationToken>()).Returns(false);

            var result = await _service.RetryBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_WhenFailedAndOwnedByRequester_PublishesJobAndReturnsAccepted()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Failed");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            _batchRepository.TryMarkFailedForRetryAsync(batchId, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _service.RetryBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Success, result.Status);
            await _messagePublisher.Received(1).PublishAsync(
                Arg.Is<ParseBatchJob>(j => j.BatchId == batchId && j.MessageId == batchId && j.ZipS3Key == batch.ZipS3Key),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_WhenAdmin_CanRetryBatchOwnedBySomeoneElse()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Failed");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            _batchRepository.TryMarkFailedForRetryAsync(batchId, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _service.RetryBatchAsync(batchId, Guid.NewGuid(), isAdmin: true, CancellationToken.None); // not the owner, but Admin

            Assert.Equal(OperationStatus.Success, result.Status);
            await _messagePublisher.Received(1).PublishAsync(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_ConcurrentCalls_OnlyOnePublishesWhenRepositoryGuardsTransition()
        {
            // Simulates two near-simultaneous retry requests: the repository's atomic
            // conditional update only lets the first one through, proving the retry
            // action itself is idempotent even under a race.
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Failed");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            _batchRepository.TryMarkFailedForRetryAsync(batchId, Arg.Any<CancellationToken>()).Returns(true, false);

            var first = await _service.RetryBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);
            var second = await _service.RetryBatchAsync(batchId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Success, first.Status);
            Assert.Equal(OperationStatus.Conflict, second.Status);
            await _messagePublisher.Received(1).PublishAsync(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }
    }
}

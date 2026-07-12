using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubmissionService.API.Controllers;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Messages;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class BatchRetryTests
    {
        private readonly IBatchRepository _batchRepository;
        private readonly IStudentPaperRepository _paperRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IS3Service _s3Service;
        private readonly BatchesController _controller;

        public BatchRetryTests()
        {
            _batchRepository = Substitute.For<IBatchRepository>();
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _publishEndpoint = Substitute.For<IPublishEndpoint>();
            _s3Service = Substitute.For<IS3Service>();

            _controller = new BatchesController(
                _batchRepository, _paperRepository, _publishEndpoint, _s3Service);
        }

        private static BatchDto MakeBatch(Guid id, Guid subjectId, Guid uploadedBy, string status) =>
            new(id, subjectId, $"submissions/{subjectId}/{id}.zip", "papers.zip", 0,
                status, uploadedBy, status == "Failed" ? "boom" : null, DateTime.UtcNow);

        private void SetUser(Guid userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("Role", role) // matches TokenService's actual claim Type, not ClaimTypes.Role
            }, "TestAuth");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        [Fact]
        public async Task RetryBatch_WhenBatchNotFound_ReturnsNotFound()
        {
            var batchId = Guid.NewGuid();
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns((BatchDto?)null);
            SetUser(Guid.NewGuid(), "Lecturer");

            var result = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
            await _publishEndpoint.DidNotReceive().Publish(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RetryBatch_WhenNotOwnerAndNotAdmin_ReturnsForbidden()
        {
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var batch = MakeBatch(batchId, subjectId, ownerId, "Failed");
            _batchRepository.GetBatchAsync(batchId, Arg.Any<CancellationToken>()).Returns(batch);
            SetUser(Guid.NewGuid(), "Lecturer"); // different user than owner

            var result = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
            await _batchRepository.DidNotReceive().TryMarkFailedForRetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _publishEndpoint.DidNotReceive().Publish(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
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
            SetUser(ownerId, "Lecturer");

            var result = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(result);
            await _publishEndpoint.DidNotReceive().Publish(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
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
            SetUser(ownerId, "Lecturer");

            var result = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
            await _publishEndpoint.Received(1).Publish(
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
            SetUser(Guid.NewGuid(), "Admin"); // not the owner, but Admin

            var result = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<AcceptedResult>(result);
            await _publishEndpoint.Received(1).Publish(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
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
            SetUser(ownerId, "Lecturer");

            var first = await _controller.RetryBatch(batchId, CancellationToken.None);
            var second = await _controller.RetryBatch(batchId, CancellationToken.None);

            Assert.IsType<AcceptedResult>(first);
            Assert.IsType<ConflictObjectResult>(second);
            await _publishEndpoint.Received(1).Publish(Arg.Any<ParseBatchJob>(), Arg.Any<CancellationToken>());
        }
    }
}

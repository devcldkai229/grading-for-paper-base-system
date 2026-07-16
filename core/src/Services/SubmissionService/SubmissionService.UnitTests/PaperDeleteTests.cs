using System;
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
    public class PaperDeleteTests
    {
        private readonly IStudentPaperRepository _paperRepository;
        private readonly IS3Service _s3Service;
        private readonly ISubmissionAuditLogRepository _auditLogRepository;
        private readonly PaperQueryService _service;

        public PaperDeleteTests()
        {
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _s3Service = Substitute.For<IS3Service>();
            _auditLogRepository = Substitute.For<ISubmissionAuditLogRepository>();

            var presignedUrlOptions = new PresignedUrlOptions { Ttl = TimeSpan.FromMinutes(5) };

            _service = new PaperQueryService(
                _paperRepository, _s3Service, _auditLogRepository, presignedUrlOptions);
        }

        private static PaperDeletionInfoDto MakePaper(
            Guid paperId, Guid batchId, Guid subjectId, Guid uploadedBy, string status, params string[] s3Keys) =>
            new(paperId, batchId, subjectId, uploadedBy, status,
                Array.ConvertAll(s3Keys, k => new PaperFileRefDto(Guid.NewGuid(), k)));

        [Fact]
        public async Task DeletePaper_WhenPaperNotFound_ReturnsNotFound()
        {
            var paperId = Guid.NewGuid();
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>())
                .Returns((PaperDeletionInfoDto?)null);

            var result = await _service.DeletePaperAsync(paperId, Guid.NewGuid(), isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.NotFound, result.Status);
            await _s3Service.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeletePaper_WhenNotOwnerAndNotAdmin_ReturnsForbidden()
        {
            var paperId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var paper = MakePaper(paperId, batchId, subjectId, ownerId, "ReadyToAssign", "k1");
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>()).Returns(paper);

            var result = await _service.DeletePaperAsync(paperId, Guid.NewGuid(), isAdmin: false, CancellationToken.None); // different user than owner

            Assert.Equal(OperationStatus.Forbidden, result.Status);
            await _s3Service.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _paperRepository.DidNotReceive().DeletePaperAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeletePaper_WhenAlreadyStartedGrading_ReturnsConflictAndDoesNotDelete()
        {
            var paperId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var paper = MakePaper(paperId, batchId, subjectId, ownerId, "Assigned", "k1");
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>()).Returns(paper);

            var result = await _service.DeletePaperAsync(paperId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Conflict, result.Status);
            await _s3Service.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await _paperRepository.DidNotReceive().DeletePaperAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeletePaper_WhenReadyToAssignAndOwner_PurgesS3AndDbAndAudits()
        {
            var paperId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var paper = MakePaper(paperId, batchId, subjectId, ownerId, "ReadyToAssign", "k1", "k2");
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>()).Returns(paper);

            var result = await _service.DeletePaperAsync(paperId, ownerId, isAdmin: false, CancellationToken.None);

            Assert.Equal(OperationStatus.Success, result.Status);
            await _s3Service.Received(1).DeleteAsync("k1", Arg.Any<CancellationToken>());
            await _s3Service.Received(1).DeleteAsync("k2", Arg.Any<CancellationToken>());
            await _paperRepository.Received(1).DeletePaperAsync(paperId, Arg.Any<CancellationToken>());
            await _auditLogRepository.Received(1).LogAsync(
                "DeletePaper", "Paper", paperId, subjectId, ownerId,
                Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeletePaper_WhenAdmin_CanDeletePaperUploadedBySomeoneElse()
        {
            var paperId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var paper = MakePaper(paperId, batchId, subjectId, ownerId, "ReadyToAssign", "k1");
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>()).Returns(paper);

            var result = await _service.DeletePaperAsync(paperId, Guid.NewGuid(), isAdmin: true, CancellationToken.None); // not the uploader, but Admin

            Assert.Equal(OperationStatus.Success, result.Status);
            await _paperRepository.Received(1).DeletePaperAsync(paperId, Arg.Any<CancellationToken>());
        }
    }
}

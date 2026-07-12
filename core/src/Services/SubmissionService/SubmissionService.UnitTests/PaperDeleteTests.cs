using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.AwsS3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using SubmissionService.API.Controllers;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class PaperDeleteTests
    {
        private readonly IStudentPaperRepository _paperRepository;
        private readonly IS3Service _s3Service;
        private readonly ISubmissionAuditLogRepository _auditLogRepository;
        private readonly SubmissionsController _controller;

        public PaperDeleteTests()
        {
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _s3Service = Substitute.For<IS3Service>();
            _auditLogRepository = Substitute.For<ISubmissionAuditLogRepository>();

            var s3Settings = Options.Create(new AwsS3Settings { PresignedUrlTtlMinutes = 5 });

            _controller = new SubmissionsController(
                _paperRepository, _s3Service, _auditLogRepository, s3Settings);
        }

        private static PaperDeletionInfoDto MakePaper(
            Guid paperId, Guid batchId, Guid subjectId, Guid uploadedBy, string status, params string[] s3Keys) =>
            new(paperId, batchId, subjectId, uploadedBy, status,
                Array.ConvertAll(s3Keys, k => new PaperFileRefDto(Guid.NewGuid(), k)));

        private void SetUser(Guid userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("Role", role)
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
        public async Task DeletePaper_WhenPaperNotFound_ReturnsNotFound()
        {
            var paperId = Guid.NewGuid();
            _paperRepository.GetPaperForDeletionAsync(paperId, Arg.Any<CancellationToken>())
                .Returns((PaperDeletionInfoDto?)null);
            SetUser(Guid.NewGuid(), "Lecturer");

            var result = await _controller.DeletePaper(paperId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
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
            SetUser(Guid.NewGuid(), "Lecturer"); // different user than owner

            var result = await _controller.DeletePaper(paperId, CancellationToken.None);

            Assert.IsType<ForbidResult>(result);
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
            SetUser(ownerId, "Lecturer");

            var result = await _controller.DeletePaper(paperId, CancellationToken.None);

            Assert.IsType<ConflictObjectResult>(result);
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
            SetUser(ownerId, "Lecturer");

            var result = await _controller.DeletePaper(paperId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
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
            SetUser(Guid.NewGuid(), "Admin"); // not the uploader, but Admin

            var result = await _controller.DeletePaper(paperId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            await _paperRepository.Received(1).DeletePaperAsync(paperId, Arg.Any<CancellationToken>());
        }
    }
}

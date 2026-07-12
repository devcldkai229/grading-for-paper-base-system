using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExamCatalogService.API;
using ExamCatalogService.API.Controllers;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using BuildingBlocks.AwsS3;
using NSubstitute;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class SubjectSearchControllerTests
    {
        private readonly IExamCatalogRepository _repository;
        private readonly IGradingServiceClient _gradingServiceClient;
        private readonly SubjectsController _controller;

        public SubjectSearchControllerTests()
        {
            _repository = Substitute.For<IExamCatalogRepository>();
            _gradingServiceClient = Substitute.For<IGradingServiceClient>();

            var s3Service = Substitute.For<IS3Service>();
            var s3Settings = Options.Create(new AwsS3Settings { PresignedUrlTtlMinutes = 10 });
            var filePreviewService = new SubjectFilePreviewService(s3Service, Substitute.For<IGotenbergClient>());

            _controller = new SubjectsController(
                _repository,
                s3Service,
                Substitute.For<IAiGradingClient>(),
                _gradingServiceClient,
                Substitute.For<ISubjectAdminService>(),
                filePreviewService,
                s3Settings);
        }

        private void SetUser(Guid userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("Role", role) // matches TokenService's actual claim Type
            }, "TestAuth");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        private static SubjectSearchResultDto MakeResult(string code) => new(
            Guid.NewGuid(), Guid.NewGuid(), "Exam 1", Guid.NewGuid(), "SP26",
            code, "Title", 10m, "Open", true, true, 5, DateTime.UtcNow);

        [Fact]
        public async Task SearchSubjects_WithInvalidStatus_ReturnsBadRequestWithoutQuerying()
        {
            SetUser(Guid.NewGuid(), "Admin");

            var result = await _controller.SearchSubjects(
                null, null, null, "NotARealStatus", 1, 20, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            await _repository.DidNotReceive().SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjects_AsAdmin_DoesNotCallGradingServiceAndPassesNullRestriction()
        {
            SetUser(Guid.NewGuid(), "Admin");
            _repository.SearchSubjectsAsync(
                    null, null, null, null, null, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto> { MakeResult("PRN222") }, 1));

            var result = await _controller.SearchSubjects(
                null, null, null, null, 1, 20, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            await _gradingServiceClient.DidNotReceive().GetAssignedSubjectIdsAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _repository.Received(1).SearchSubjectsAsync(
                null, null, null, null,
                Arg.Is<IReadOnlySet<Guid>?>(r => r == null),
                1, 20, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjects_AsLecturer_RestrictsToAssignedSubjectIds()
        {
            var lecturerId = Guid.NewGuid();
            var assignedId = Guid.NewGuid();
            SetUser(lecturerId, "Lecturer");
            _gradingServiceClient.GetAssignedSubjectIdsAsync(lecturerId, Arg.Any<CancellationToken>())
                .Returns(new List<Guid> { assignedId });
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            await _controller.SearchSubjects(null, null, null, null, 1, 20, CancellationToken.None);

            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Is<IReadOnlySet<Guid>?>(r => r != null && r.Count == 1 && r.Contains(assignedId)),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjects_AsLecturer_WhenGradingServiceUnreachable_FailsClosedToEmptyRestriction()
        {
            var lecturerId = Guid.NewGuid();
            SetUser(lecturerId, "Lecturer");
            _gradingServiceClient.GetAssignedSubjectIdsAsync(lecturerId, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<Guid>?)null); // unreachable
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            await _controller.SearchSubjects(null, null, null, null, 1, 20, CancellationToken.None);

            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Is<IReadOnlySet<Guid>?>(r => r != null && r.Count == 0),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjects_ParsesStatusCaseInsensitively()
        {
            SetUser(Guid.NewGuid(), "Admin");
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), SubjectStatus.Open,
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            var result = await _controller.SearchSubjects(
                null, null, null, "open", 1, 20, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), SubjectStatus.Open,
                Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjects_ClampsPageAndPageSize()
        {
            SetUser(Guid.NewGuid(), "Admin");
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            await _controller.SearchSubjects(null, null, null, null, page: 0, pageSize: 500, CancellationToken.None);

            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Any<IReadOnlySet<Guid>?>(), 1, 20, Arg.Any<CancellationToken>());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Application.Services;
using ExamCatalogService.Domain.Enums;
using NSubstitute;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class SubjectSearchControllerTests
    {
        private readonly IExamCatalogRepository _repository;
        private readonly IGradingServiceClient _gradingServiceClient;
        private readonly SubjectQueryService _service;

        public SubjectSearchControllerTests()
        {
            _repository = Substitute.For<IExamCatalogRepository>();
            _gradingServiceClient = Substitute.For<IGradingServiceClient>();

            _service = new SubjectQueryService(_repository, _gradingServiceClient);
        }

        private static SubjectSearchResultDto MakeResult(string code) => new(
            Guid.NewGuid(), Guid.NewGuid(), "Exam 1", Guid.NewGuid(), "SP26",
            code, "Title", 10m, "Open", true, true, 5, DateTime.UtcNow);

        [Fact]
        public async Task SearchSubjectsAsync_WithInvalidStatus_ReturnsErrorWithoutQuerying()
        {
            var result = await _service.SearchSubjectsAsync(
                null, null, null, "NotARealStatus", lecturerId: null, page: 1, pageSize: 20, CancellationToken.None);

            Assert.NotNull(result.Error);
            await _repository.DidNotReceive().SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjectsAsync_WhenLecturerIdIsNull_DoesNotCallGradingServiceAndPassesNullRestriction()
        {
            _repository.SearchSubjectsAsync(
                    null, null, null, null, null, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto> { MakeResult("PRN222") }, 1));

            var result = await _service.SearchSubjectsAsync(
                null, null, null, null, lecturerId: null, page: 1, pageSize: 20, CancellationToken.None);

            Assert.Null(result.Error);
            await _gradingServiceClient.DidNotReceive().GetAssignedSubjectIdsAsync(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _repository.Received(1).SearchSubjectsAsync(
                null, null, null, null,
                Arg.Is<IReadOnlySet<Guid>?>(r => r == null),
                1, 20, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjectsAsync_WhenLecturerIdGiven_RestrictsToAssignedSubjectIds()
        {
            var lecturerId = Guid.NewGuid();
            var assignedId = Guid.NewGuid();
            _gradingServiceClient.GetAssignedSubjectIdsAsync(lecturerId, Arg.Any<CancellationToken>())
                .Returns(new List<Guid> { assignedId });
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            await _service.SearchSubjectsAsync(
                null, null, null, null, lecturerId, page: 1, pageSize: 20, CancellationToken.None);

            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Is<IReadOnlySet<Guid>?>(r => r != null && r.Count == 1 && r.Contains(assignedId)),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjectsAsync_WhenGradingServiceUnreachable_FailsClosedToEmptyRestriction()
        {
            var lecturerId = Guid.NewGuid();
            _gradingServiceClient.GetAssignedSubjectIdsAsync(lecturerId, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<Guid>?)null); // unreachable
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            await _service.SearchSubjectsAsync(
                null, null, null, null, lecturerId, page: 1, pageSize: 20, CancellationToken.None);

            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Is<IReadOnlySet<Guid>?>(r => r != null && r.Count == 0),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjectsAsync_ParsesStatusCaseInsensitively()
        {
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), SubjectStatus.Open,
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            var result = await _service.SearchSubjectsAsync(
                null, null, null, "open", lecturerId: null, page: 1, pageSize: 20, CancellationToken.None);

            Assert.Null(result.Error);
            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), SubjectStatus.Open,
                Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchSubjectsAsync_ClampsPageAndPageSize()
        {
            _repository.SearchSubjectsAsync(
                    Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                    Arg.Any<IReadOnlySet<Guid>?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((new List<SubjectSearchResultDto>(), 0));

            var result = await _service.SearchSubjectsAsync(
                null, null, null, null, lecturerId: null, page: 0, pageSize: 500, CancellationToken.None);

            Assert.Equal(1, result.Page);
            Assert.Equal(20, result.PageSize);
            await _repository.Received(1).SearchSubjectsAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<SubjectStatus?>(),
                Arg.Any<IReadOnlySet<Guid>?>(), 1, 20, Arg.Any<CancellationToken>());
        }
    }
}

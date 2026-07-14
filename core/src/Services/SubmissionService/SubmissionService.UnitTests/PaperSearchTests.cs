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
    public class PaperSearchTests
    {
        private readonly IStudentPaperRepository _paperRepository;
        private readonly PaperQueryService _service;

        public PaperSearchTests()
        {
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            var s3Service = Substitute.For<IS3Service>();
            var auditLogRepository = Substitute.For<ISubmissionAuditLogRepository>();
            var presignedUrlOptions = new PresignedUrlOptions { Ttl = TimeSpan.FromMinutes(5) };

            _service = new PaperQueryService(_paperRepository, s3Service, auditLogRepository, presignedUrlOptions);
        }

        private static StudentPaperDto MakePaper(string alias) =>
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alias, 1, "ReadyToAssign", 1, DateTime.UtcNow);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SearchPapersAsync_WhenKeywordBlank_ReturnsEmptyPageWithoutCallingRepository(string? keyword)
        {
            var result = await _service.SearchPapersAsync(keyword, 1, 20, null, CancellationToken.None);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            await _paperRepository.DidNotReceive().SearchPapersAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchPapersAsync_WithKeyword_DelegatesToRepositoryAndReturnsResults()
        {
            _paperRepository.SearchPapersAsync("Student_0007", 1, 20, null, Arg.Any<CancellationToken>())
                .Returns((new List<StudentPaperDto> { MakePaper("Student_0007") }, 1));

            var result = await _service.SearchPapersAsync("Student_0007", 1, 20, null, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task SearchPapersAsync_TrimsKeywordBeforeDelegating()
        {
            await _service.SearchPapersAsync("  Student_0007  ", 1, 20, null, CancellationToken.None);

            await _paperRepository.Received(1).SearchPapersAsync(
                "Student_0007", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SearchPapersAsync_PassesUploadedByFilterThrough()
        {
            var lecturerId = Guid.NewGuid();

            await _service.SearchPapersAsync("7", 1, 20, lecturerId, CancellationToken.None);

            await _paperRepository.Received(1).SearchPapersAsync(
                "7", Arg.Any<int>(), Arg.Any<int>(), lecturerId, Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(3, 3)]
        public async Task SearchPapersAsync_ClampsPageToAtLeastOne(int requestedPage, int expectedPage)
        {
            await _service.SearchPapersAsync("keyword", requestedPage, 20, null, CancellationToken.None);

            await _paperRepository.Received(1).SearchPapersAsync(
                "keyword", expectedPage, Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(0, 20)]
        [InlineData(500, 20)]
        [InlineData(50, 50)]
        public async Task SearchPapersAsync_ClampsPageSizeTo1To100(int requestedPageSize, int expectedPageSize)
        {
            await _service.SearchPapersAsync("keyword", 1, requestedPageSize, null, CancellationToken.None);

            await _paperRepository.Received(1).SearchPapersAsync(
                "keyword", Arg.Any<int>(), expectedPageSize, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        }
    }
}

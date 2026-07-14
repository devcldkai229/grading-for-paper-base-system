using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using ReportingService.Application.Services;
using Xunit;

namespace ReportingService.UnitTests
{
    public class GlobalAuditLogServiceTests
    {
        private readonly IIamServiceClient _iamClient = Substitute.For<IIamServiceClient>();
        private readonly IGradingServiceClient _gradingClient = Substitute.For<IGradingServiceClient>();
        private readonly ISubmissionServiceClient _submissionClient = Substitute.For<ISubmissionServiceClient>();
        private readonly GlobalAuditLogService _service;

        public GlobalAuditLogServiceTests()
        {
            _service = new GlobalAuditLogService(_iamClient, _gradingClient, _submissionClient);

            // Default: all sources empty, so tests only need to stub the source(s) they care about.
            _iamClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new IamAuditLogPageClientDto(new List<IamAuditLogEntryClientDto>(), 1, 50, 0));
            _gradingClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new GradingAuditLogPageClientDto(new List<GradingAuditLogEntryClientDto>(), 0));
            _submissionClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new SubmissionAuditLogPageClientDto(new List<SubmissionAuditLogEntryClientDto>(), 0));
        }

        [Fact]
        public async Task GetAuditLogsAsync_MergesAllThreeSourcesSortedNewestFirst()
        {
            var now = DateTime.UtcNow;
            _iamClient.GetAuditLogsAsync(null, null, null, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new IamAuditLogPageClientDto(
                    new List<IamAuditLogEntryClientDto>
                    {
                        new(Guid.NewGuid(), Guid.NewGuid(), "Create", "User", Guid.NewGuid(), null, null, now.AddMinutes(-10))
                    }, 1, 50, 1));
            _gradingClient.GetAuditLogsAsync(null, null, null, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new GradingAuditLogPageClientDto(
                    new List<GradingAuditLogEntryClientDto>
                    {
                        new(Guid.NewGuid(), Guid.NewGuid(), "ScoreOverridden", "GradingForm", Guid.NewGuid(), "5", "8", "typo fix", now)
                    }, 1));
            _submissionClient.GetAuditLogsAsync(null, null, null, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new SubmissionAuditLogPageClientDto(
                    new List<SubmissionAuditLogEntryClientDto>
                    {
                        new(Guid.NewGuid(), "DeleteBatch", "Batch", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "deleted batch", now.AddMinutes(-5))
                    }, 1));

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            Assert.Equal(3, result.TotalCount);
            Assert.Equal(3, result.Items.Count);
            Assert.Equal("Grading", result.Items[0].Source); // now (newest)
            Assert.Equal("Submission", result.Items[1].Source); // now - 5m
            Assert.Equal("Iam", result.Items[2].Source); // now - 10m (oldest)
            Assert.Empty(result.UnavailableSources);
        }

        [Fact]
        public async Task GetAuditLogsAsync_WhenOneSourceUnreachable_SkipsItButReturnsOthers()
        {
            _gradingClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((GradingAuditLogPageClientDto?)null);
            _iamClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new IamAuditLogPageClientDto(
                    new List<IamAuditLogEntryClientDto>
                    {
                        new(Guid.NewGuid(), Guid.NewGuid(), "Create", "User", Guid.NewGuid(), null, null, DateTime.UtcNow)
                    }, 1, 50, 1));

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(1, result.TotalCount);
            Assert.Contains("GradingService", result.UnavailableSources);
        }

        [Fact]
        public async Task GetAuditLogsAsync_WhenAllSourcesUnreachable_ReturnsEmptyWithAllUnavailable()
        {
            _iamClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((IamAuditLogPageClientDto?)null);
            _gradingClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((GradingAuditLogPageClientDto?)null);
            _submissionClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((SubmissionAuditLogPageClientDto?)null);

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.Equal(3, result.UnavailableSources.Count);
        }

        [Fact]
        public async Task GetAuditLogsAsync_PaginatesTheMergedList()
        {
            var now = DateTime.UtcNow;
            _iamClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new IamAuditLogPageClientDto(
                    Enumerable.Range(0, 5)
                        .Select(i => new IamAuditLogEntryClientDto(
                            Guid.NewGuid(), Guid.NewGuid(), "Create", "User", Guid.NewGuid(), null, null, now.AddMinutes(-i)))
                        .ToList(),
                    1, 50, 5));

            var page1 = await _service.GetAuditLogsAsync(null, null, null, 1, 2, CancellationToken.None);
            var page2 = await _service.GetAuditLogsAsync(null, null, null, 2, 2, CancellationToken.None);

            Assert.Equal(2, page1.Items.Count);
            Assert.Equal(2, page2.Items.Count);
            Assert.Equal(5, page1.TotalCount);
            // newest-first: page1 = [now, now-1], page2 = [now-2, now-3]
            Assert.Equal(now, page1.Items[0].PerformedAt, TimeSpan.FromSeconds(1));
            Assert.Equal(now.AddMinutes(-2), page2.Items[0].PerformedAt, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetAuditLogsAsync_FormatsGradingDetailsWithOldNewAndReason()
        {
            _gradingClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new GradingAuditLogPageClientDto(
                    new List<GradingAuditLogEntryClientDto>
                    {
                        new(Guid.NewGuid(), Guid.NewGuid(), "ScoreOverridden", "GradingForm", Guid.NewGuid(), "5", "8", "typo fix", DateTime.UtcNow)
                    }, 1));

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            var entry = Assert.Single(result.Items);
            Assert.Contains("5", entry.Details);
            Assert.Contains("8", entry.Details);
            Assert.Contains("typo fix", entry.Details);
        }

        [Fact]
        public async Task GetAuditLogsAsync_PassesFiltersThroughToEverySource()
        {
            var userId = Guid.NewGuid();

            await _service.GetAuditLogsAsync(userId, "User", "Create", 1, 20, CancellationToken.None);

            await _iamClient.Received(1).GetAuditLogsAsync(userId, "User", "Create", 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _gradingClient.Received(1).GetAuditLogsAsync(userId, "User", "Create", 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _submissionClient.Received(1).GetAuditLogsAsync(userId, "User", "Create", 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }
}

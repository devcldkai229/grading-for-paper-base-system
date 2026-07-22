using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using ReportingService.Application.Services;
using ReportingService.Domain.Entities;
using Xunit;

namespace ReportingService.UnitTests
{
    public class GlobalAuditLogServiceTests
    {
        private readonly IAuditRecordReadRepository _auditRepository = Substitute.For<IAuditRecordReadRepository>();
        private readonly ISubmissionServiceClient _submissionClient = Substitute.For<ISubmissionServiceClient>();
        private readonly GlobalAuditLogService _service;

        public GlobalAuditLogServiceTests()
        {
            _service = new GlobalAuditLogService(_auditRepository, _submissionClient);

            // Default: local audit projection empty + Submission empty, so each test stubs only what it needs.
            _auditRepository.QueryAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditRecord>)new List<AuditRecord>(), 0));
            _submissionClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new SubmissionAuditLogPageClientDto(new List<SubmissionAuditLogEntryClientDto>(), 0));
        }

        private static AuditRecord Record(string source, string action, DateTime occurredAt,
            string? oldValue = null, string? newValue = null, string? reason = null) =>
            new()
            {
                MessageId = Guid.NewGuid(),
                SourceService = source,
                UserId = Guid.NewGuid(),
                Action = action,
                EntityType = "User",
                EntityId = Guid.NewGuid(),
                OldValue = oldValue,
                NewValue = newValue,
                Reason = reason,
                OccurredAt = occurredAt
            };

        [Fact]
        public async Task GetAuditLogsAsync_MergesLocalAndSubmissionSortedNewestFirst()
        {
            var now = DateTime.UtcNow;
            _auditRepository.QueryAsync(null, null, null, Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditRecord>)new List<AuditRecord>
                {
                    Record("grading", "ScoreOverridden", now, "5", "8", "typo fix"),
                    Record("iam", "Create", now.AddMinutes(-10))
                }, 2));
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
        public async Task GetAuditLogsAsync_WhenSubmissionUnreachable_SkipsItButReturnsLocalEntries()
        {
            _auditRepository.QueryAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditRecord>)new List<AuditRecord>
                {
                    Record("iam", "Create", DateTime.UtcNow)
                }, 1));
            _submissionClient.GetAuditLogsAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((SubmissionAuditLogPageClientDto?)null);

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(1, result.TotalCount);
            Assert.Contains("SubmissionService", result.UnavailableSources);
        }

        [Fact]
        public async Task GetAuditLogsAsync_PaginatesTheMergedList()
        {
            var now = DateTime.UtcNow;
            _auditRepository.QueryAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditRecord>)Enumerable.Range(0, 5)
                    .Select(i => Record("iam", "Create", now.AddMinutes(-i)))
                    .ToList(), 5));

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
            _auditRepository.QueryAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditRecord>)new List<AuditRecord>
                {
                    Record("grading", "ScoreOverridden", DateTime.UtcNow, "5", "8", "typo fix")
                }, 1));

            var result = await _service.GetAuditLogsAsync(null, null, null, 1, 20, CancellationToken.None);

            var entry = Assert.Single(result.Items);
            Assert.Contains("5", entry.Details);
            Assert.Contains("8", entry.Details);
            Assert.Contains("typo fix", entry.Details);
        }

        [Fact]
        public async Task GetAuditLogsAsync_PassesFiltersThroughToBothSources()
        {
            var userId = Guid.NewGuid();

            await _service.GetAuditLogsAsync(userId, "User", "Create", 1, 20, CancellationToken.None);

            await _auditRepository.Received(1).QueryAsync(userId, "User", "Create", Arg.Any<int>(), Arg.Any<CancellationToken>());
            await _submissionClient.Received(1).GetAuditLogsAsync(userId, "User", "Create", 1, Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }
}

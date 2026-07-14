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
    public class PassFailReportServiceTests
    {
        private static SubjectFilterResultClientDto MakeSubject(
            Guid id, decimal? passScore, decimal maxScore = 10m, string code = "PRN232") =>
            new(id, code, "Title", Guid.NewGuid(), "FE Exam", Guid.NewGuid(), "SP26", maxScore, passScore);

        private static SubjectScoreDistributionClientDto MakeDistribution(Guid subjectId, params decimal[] scores) =>
            new(subjectId, scores.Length,
                scores.Length > 0 ? Math.Round(scores.Average(), 2) : null,
                scores.Length > 0 ? scores.Min() : null,
                scores.Length > 0 ? scores.Max() : null,
                scores);

        [Fact]
        public async Task GetReportAsync_WhenCatalogUnreachable_ReturnsError()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<SubjectFilterResultClientDto>?)null);
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task GetReportAsync_WhenGradingServiceUnreachable_ReturnsError()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, passScore: 5m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns((ScoreDistributionDashboardClientDto?)null);
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task GetReportAsync_WhenNoPassScoreConfigured_CountsAndRateAreNull()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, passScore: null) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 8m, 3m)
                }));
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Null(subject.PassScore);
            Assert.Null(subject.PassCount);
            Assert.Null(subject.FailCount);
            Assert.Null(subject.PassRatePercent);
            Assert.Equal(2, subject.SubmittedCount); // raw count still reported
        }

        [Fact]
        public async Task GetReportAsync_SplitsScoresAgainstThreshold_AndComputesRate()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, passScore: 5m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 8m, 3m, 6m, 4m) // pass: 8,6 ; fail: 3,4
                }));
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(2, subject.PassCount);
            Assert.Equal(2, subject.FailCount);
            Assert.Equal(50m, subject.PassRatePercent);
        }

        [Fact]
        public async Task GetReportAsync_ScoreExactlyAtThreshold_CountsAsPass()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, passScore: 5m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 5m)
                }));
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(1, subject.PassCount);
            Assert.Equal(0, subject.FailCount);
            Assert.Equal(100m, subject.PassRatePercent);
        }

        [Fact]
        public async Task GetReportAsync_WhenThresholdConfiguredButNoSubmissions_CountsAreZeroAndRateIsNull()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, passScore: 5m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>()));
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(0, subject.PassCount);
            Assert.Equal(0, subject.FailCount);
            Assert.Null(subject.PassRatePercent); // 0/0 is undefined, not 0%
        }

        [Fact]
        public async Task GetReportAsync_WhenNoSubjectsMatchFilter_ReturnsEmptyWithoutCallingGradingService()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto>());
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new PassFailReportService(catalogClient, gradingClient);

            var (result, error) = await service.GetReportAsync(Guid.NewGuid(), null, CancellationToken.None);

            Assert.Null(error);
            Assert.Empty(result!.Subjects);
            await gradingClient.DidNotReceive().GetScoreDistributionAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }
    }
}

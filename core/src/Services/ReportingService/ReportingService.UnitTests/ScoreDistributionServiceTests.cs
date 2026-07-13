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
    public class ScoreDistributionServiceTests
    {
        private static SubjectFilterResultClientDto MakeSubject(Guid id, decimal maxScore = 10m, string code = "PRN232") =>
            new(id, code, "Title", Guid.NewGuid(), "FE Exam", Guid.NewGuid(), "SP26", maxScore);

        private static SubjectScoreDistributionClientDto MakeDistribution(Guid subjectId, params decimal[] scores) =>
            new(subjectId, scores.Length,
                scores.Length > 0 ? Math.Round(scores.Average(), 2) : null,
                scores.Length > 0 ? scores.Min() : null,
                scores.Length > 0 ? scores.Max() : null,
                scores);

        [Fact]
        public async Task GetDistributionAsync_WhenCatalogUnreachable_ReturnsError()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<SubjectFilterResultClientDto>?)null);
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task GetDistributionAsync_WhenGradingServiceUnreachable_ReturnsError()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns((ScoreDistributionDashboardClientDto?)null);
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task GetDistributionAsync_WhenSubjectHasNoSubmittedPapers_ReturnsAllZeroHistogram()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>()));
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(0, subject.SubmittedCount);
            Assert.Equal(10, subject.Histogram.Count);
            Assert.All(subject.Histogram, b => Assert.Equal(0, b.Count));
        }

        [Fact]
        public async Task GetDistributionAsync_BucketsScoresIntoTenEqualWidthRanges()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, maxScore: 10m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            // 0.5 -> bucket [0,1); 5.0 -> bucket [5,6); 9.9 -> bucket [9,10)
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 0.5m, 5.0m, 9.9m)
                }));
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var histogram = Assert.Single(result!.Subjects).Histogram;
            Assert.Equal(10, histogram.Count);
            Assert.Equal(1, histogram[0].Count); // [0,1)
            Assert.Equal(1, histogram[5].Count); // [5,6)
            Assert.Equal(1, histogram[9].Count); // [9,10)
            Assert.Equal(0m, histogram[0].RangeStart);
            Assert.Equal(1m, histogram[0].RangeEnd);
        }

        [Fact]
        public async Task GetDistributionAsync_ScoreExactlyAtMaxScore_LandsInLastBucketNotOverflow()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, maxScore: 10m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 10m)
                }));
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var histogram = Assert.Single(result!.Subjects).Histogram;
            Assert.Equal(1, histogram[9].Count); // last bucket, not a dropped/overflowed count
            Assert.Equal(1, histogram.Sum(b => b.Count));
        }

        [Fact]
        public async Task GetDistributionAsync_UsesSubjectSpecificMaxScoreForBucketWidth()
        {
            // A subject with maxScore=100 must bucket by tens (0-10, 10-20, ...), not by ones.
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, maxScore: 100m) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 55m)
                }));
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var histogram = Assert.Single(result!.Subjects).Histogram;
            Assert.Equal(10m, histogram[0].RangeEnd);
            Assert.Equal(1, histogram[5].Count); // [50,60)
        }

        [Fact]
        public async Task GetDistributionAsync_MergesSubjectMetadataWithStats()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, code: "PRN232") });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetScoreDistributionAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new ScoreDistributionDashboardClientDto(new List<SubjectScoreDistributionClientDto>
                {
                    MakeDistribution(subjectId, 4m, 8m)
                }));
            var service = new ScoreDistributionService(catalogClient, gradingClient);

            var (result, error) = await service.GetDistributionAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal("PRN232", subject.SubjectCode);
            Assert.Equal(2, subject.SubmittedCount);
            Assert.Equal(6m, subject.ScoreAvg);
            Assert.Equal(4m, subject.ScoreMin);
            Assert.Equal(8m, subject.ScoreMax);
        }
    }
}

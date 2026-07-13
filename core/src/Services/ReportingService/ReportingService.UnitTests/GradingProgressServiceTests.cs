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
    public class GradingProgressServiceTests
    {
        private static SubjectFilterResultClientDto MakeSubject(Guid id, string code = "PRN232") =>
            new(id, code, "Title", Guid.NewGuid(), "FE Exam", Guid.NewGuid(), "SP26", 10m);

        private static SubjectProgressClientDto MakeProgress(Guid subjectId, int total, int completed) =>
            new(subjectId, total, completed, 0, total - completed,
                total == 0 ? 0 : Math.Round(100m * completed / total, 1),
                null, null, null, null, null,
                new List<LecturerProgressClientDto>());

        [Fact]
        public async Task GetDashboardAsync_WhenCatalogUnreachable_ReturnsError()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<SubjectFilterResultClientDto>?)null);
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new GradingProgressService(catalogClient, gradingClient);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            await gradingClient.DidNotReceive().GetProgressDashboardAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetDashboardAsync_WhenGradingServiceUnreachable_ReturnsError()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetProgressDashboardAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns((GradingProgressDashboardClientDto?)null);
            var service = new GradingProgressService(catalogClient, gradingClient);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task GetDashboardAsync_WhenNoSubjectsMatchFilter_ReturnsEmptyResultWithoutCallingGradingService()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto>());
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new GradingProgressService(catalogClient, gradingClient);

            var (result, error) = await service.GetDashboardAsync(Guid.NewGuid(), null, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Empty(result!.Subjects);
            await gradingClient.DidNotReceive().GetProgressDashboardAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetDashboardAsync_MergesSubjectMetadataWithProgressNumbers()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, "PRN232") });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetProgressDashboardAsync(
                    Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == subjectId), Arg.Any<CancellationToken>())
                .Returns(new GradingProgressDashboardClientDto(new List<SubjectProgressClientDto>
                {
                    MakeProgress(subjectId, total: 10, completed: 4)
                }));
            var service = new GradingProgressService(catalogClient, gradingClient);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal("PRN232", subject.SubjectCode);
            Assert.Equal(10, subject.TotalPapers);
            Assert.Equal(4, subject.CompletedPapers);
            Assert.Equal(40m, subject.CompletionPercent);
        }

        [Fact]
        public async Task GetDashboardAsync_WhenSubjectHasNoAssignmentsYet_IncludesItWithZeroProgress()
        {
            // A subject the filter matched but that GradingService has no rows for yet
            // (grading hasn't started) must still appear — not be silently dropped.
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetProgressDashboardAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new GradingProgressDashboardClientDto(new List<SubjectProgressClientDto>()));
            var service = new GradingProgressService(catalogClient, gradingClient);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(0, subject.TotalPapers);
            Assert.Equal(0m, subject.CompletionPercent);
            Assert.Empty(subject.Lecturers);
        }
    }
}

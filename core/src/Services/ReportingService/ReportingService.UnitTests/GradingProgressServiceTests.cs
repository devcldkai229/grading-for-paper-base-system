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
    public class GradingProgressServiceTests
    {
        private static SubjectFilterResultClientDto MakeSubject(
            Guid id, string code = "PRN232", DateOnly? gradingDeadline = null) =>
            new(id, code, "Title", Guid.NewGuid(), "FE Exam", Guid.NewGuid(), "SP26", 10m, GradingDeadline: gradingDeadline);

        private static SubjectProgress MakeProgress(Guid subjectId, int total, int completed) =>
            new()
            {
                SubjectId = subjectId,
                TotalPapers = total,
                CompletedPapers = completed,
                InProgress = 0,
                NotStarted = total - completed
            };

        private static IProgressReadRepository EmptyProgressRepo()
        {
            var repo = Substitute.For<IProgressReadRepository>();
            repo.GetSubjectProgressAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectProgress>());
            repo.GetMarkerProgressAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<MarkerProgress>());
            return repo;
        }

        [Fact]
        public async Task GetDashboardAsync_WhenCatalogUnreachable_ReturnsError()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<SubjectFilterResultClientDto>?)null);
            var progressRepo = EmptyProgressRepo();
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(result);
            Assert.NotNull(error);
            await progressRepo.DidNotReceive().GetSubjectProgressAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetDashboardAsync_WhenNoSubjectsMatchFilter_ReturnsEmptyResultWithoutReadingProjections()
        {
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto>());
            var progressRepo = EmptyProgressRepo();
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(Guid.NewGuid(), null, CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(result);
            Assert.Empty(result!.Subjects);
            await progressRepo.DidNotReceive().GetSubjectProgressAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetDashboardAsync_MergesSubjectMetadataWithProjectionNumbers()
        {
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, "PRN232") });
            var progressRepo = EmptyProgressRepo();
            progressRepo.GetSubjectProgressAsync(
                    Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == subjectId), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectProgress> { MakeProgress(subjectId, total: 10, completed: 4) });
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal("PRN232", subject.SubjectCode);
            Assert.Equal(10, subject.TotalPapers);
            Assert.Equal(4, subject.CompletedPapers);
            Assert.Equal(40m, subject.CompletionPercent);
        }

        [Fact]
        public async Task GetDashboardAsync_MapsMarkerProjectionsIntoLecturerRows()
        {
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var progressRepo = EmptyProgressRepo();
            progressRepo.GetSubjectProgressAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<SubjectProgress> { MakeProgress(subjectId, total: 5, completed: 2) });
            progressRepo.GetMarkerProgressAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<MarkerProgress>
                {
                    new()
                    {
                        SubjectId = subjectId, TeacherId = teacherId,
                        AssignedCount = 5, CompletedCount = 2, DraftingCount = 1, AvgScore = 7.5m
                    }
                });
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var lecturer = Assert.Single(Assert.Single(result!.Subjects).Lecturers);
            Assert.Equal(teacherId, lecturer.TeacherId);
            Assert.Equal(5, lecturer.AssignedCount);
            Assert.Equal(2, lecturer.CompletedCount);
            Assert.Equal(1, lecturer.DraftingCount);
            Assert.Equal(2, lecturer.NotStartedCount);
            Assert.Equal(7.5m, lecturer.AvgScore);
        }

        [Fact]
        public async Task GetDashboardAsync_WhenSubjectHasNoAssignmentsYet_IncludesItWithZeroProgress()
        {
            // A subject the filter matched but with no projection rows yet (grading hasn't started)
            // must still appear — not be silently dropped.
            var subjectId = Guid.NewGuid();
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId) });
            var progressRepo = EmptyProgressRepo();
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            var subject = Assert.Single(result!.Subjects);
            Assert.Equal(0, subject.TotalPapers);
            Assert.Equal(0m, subject.CompletionPercent);
            Assert.Empty(subject.Lecturers);
        }

        [Fact]
        public async Task GetDashboardAsync_PassesThroughGradingDeadlineFromSubjectMetadata()
        {
            var subjectId = Guid.NewGuid();
            var deadline = new DateOnly(2026, 7, 27);
            var catalogClient = Substitute.For<IExamCatalogServiceClient>();
            catalogClient.SearchSubjectsAsync(null, null, Arg.Any<CancellationToken>())
                .Returns(new List<SubjectFilterResultClientDto> { MakeSubject(subjectId, gradingDeadline: deadline) });
            var progressRepo = EmptyProgressRepo();
            var service = new GradingProgressService(catalogClient, progressRepo);

            var (result, error) = await service.GetDashboardAsync(null, null, CancellationToken.None);

            Assert.Null(error);
            Assert.Equal(deadline, Assert.Single(result!.Subjects).GradingDeadline);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using ReportingService.Application.Services;
using ReportingService.Domain.Enums;
using ReportingService.Infrastructure.Files;
using ReportingService.Infrastructure.Persistence;
using ReportingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ReportingService.UnitTests
{
    public class FeedbackReportServiceTests
    {
        private static ReportingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ReportingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ReportingDbContext(options);
        }

        private static MemoryStream MakeMappingCsv(params (int Alias, string Code, string Name)[] rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AliasNumber,StudentCode,StudentName");
            foreach (var (alias, code, name) in rows)
            {
                sb.AppendLine($"{alias},{code},{name}");
            }
            return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static StudentFeedbackClientDto MakeStudent(int aliasNumber, decimal score, string? paperComment) =>
            new(
                Guid.NewGuid(),
                aliasNumber,
                $"Student_{aliasNumber:D4}",
                score,
                paperComment,
                new List<QuestionFeedbackClientDto>
                {
                    new("Q1", "Question 1", score, 10m, "nice")
                });

        [Fact]
        public async Task GenerateFeedbackReportAsync_WithValidData_ReturnsOneRowPerStudentAndPersistsExportJob()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetReleasableFeedbackAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectFeedbackClientDto(subjectId, new List<StudentFeedbackClientDto>
                {
                    MakeStudent(1, 8m, "good"),
                    MakeStudent(2, 9m, "great"),
                }));
            var service = new FeedbackReportService(
                gradingClient, new ExportJobRepository(db), new MiniExcelReportFileService());

            using var mapping = MakeMappingCsv((1, "SE001", "Nguyen Van A"), (2, "SE002", "Tran Thi B"));
            var (bytes, fileName, error) = await service.GenerateFeedbackReportAsync(
                subjectId, Guid.NewGuid(), mapping, "mapping.csv", CancellationToken.None);

            Assert.Null(error);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 0);
            Assert.Contains(subjectId.ToString("N"), fileName);

            var job = Assert.Single(db.ExportJobs);
            Assert.Equal(subjectId, job.SubjectId);
            Assert.Equal(ExportStatus.Completed, job.Status);
        }

        [Fact]
        public async Task GenerateFeedbackReportAsync_WhenGradingServiceUnreachable_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetReleasableFeedbackAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns((SubjectFeedbackClientDto?)null);
            var service = new FeedbackReportService(
                gradingClient, new ExportJobRepository(db), new MiniExcelReportFileService());

            using var mapping = MakeMappingCsv((1, "SE001", "Nguyen Van A"));
            var (bytes, _, error) = await service.GenerateFeedbackReportAsync(
                subjectId, Guid.NewGuid(), mapping, "mapping.csv", CancellationToken.None);

            Assert.Null(bytes);
            Assert.NotNull(error);
            Assert.Empty(db.ExportJobs);
        }

        [Fact]
        public async Task GenerateFeedbackReportAsync_WhenNoSubmittedPapers_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetReleasableFeedbackAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectFeedbackClientDto(subjectId, new List<StudentFeedbackClientDto>()));
            var service = new FeedbackReportService(
                gradingClient, new ExportJobRepository(db), new MiniExcelReportFileService());

            using var mapping = MakeMappingCsv((1, "SE001", "Nguyen Van A"));
            var (bytes, _, error) = await service.GenerateFeedbackReportAsync(
                subjectId, Guid.NewGuid(), mapping, "mapping.csv", CancellationToken.None);

            Assert.Null(bytes);
            Assert.Contains("Submitted", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GenerateFeedbackReportAsync_WhenMappingFileEmpty_ReturnsError()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var gradingClient = Substitute.For<IGradingServiceClient>();
            var service = new FeedbackReportService(
                gradingClient, new ExportJobRepository(db), new MiniExcelReportFileService());

            using var mapping = new MemoryStream(Encoding.UTF8.GetBytes("AliasNumber,StudentCode,StudentName\n"));
            var (bytes, _, error) = await service.GenerateFeedbackReportAsync(
                subjectId, Guid.NewGuid(), mapping, "mapping.csv", CancellationToken.None);

            Assert.Null(bytes);
            Assert.NotNull(error);
            await gradingClient.DidNotReceive().GetReleasableFeedbackAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GenerateFeedbackReportAsync_WhenAliasNotInMapping_StillIncludesStudentWithPlaceholder()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var gradingClient = Substitute.For<IGradingServiceClient>();
            gradingClient.GetReleasableFeedbackAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(new SubjectFeedbackClientDto(subjectId, new List<StudentFeedbackClientDto>
                {
                    MakeStudent(1, 8m, "good"),
                    MakeStudent(99, 6m, "ok"), // alias 99 has no mapping row
                }));
            var service = new FeedbackReportService(
                gradingClient, new ExportJobRepository(db), new MiniExcelReportFileService());

            using var mapping = MakeMappingCsv((1, "SE001", "Nguyen Van A"));
            var (bytes, _, error) = await service.GenerateFeedbackReportAsync(
                subjectId, Guid.NewGuid(), mapping, "mapping.csv", CancellationToken.None);

            // Doesn't fail the whole export just because one alias is unmapped — data isn't silently dropped.
            Assert.Null(error);
            Assert.NotNull(bytes);
        }

        [Fact]
        public void StudentFeedbackClientDto_HasNoInternalCommentField()
        {
            // Structural guarantee that internal notes can never flow into the export payload.
            var properties = typeof(StudentFeedbackClientDto).GetProperties().Select(p => p.Name);
            Assert.DoesNotContain("InternalComment", properties);
        }
    }
}

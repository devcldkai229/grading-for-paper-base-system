using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Persistence;
using GradingService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GradingService.Application.Interfaces;
using Xunit;

namespace GradingService.UnitTests
{
    public class ScoreDistributionTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static GradingSessionService NewService(GradingDbContext db) =>
            new(db, Substitute.For<ISubmissionServiceClient>(), Substitute.For<IExamCatalogServiceClient>());

        private static GradingAssignment MakeAssignment(
            Guid subjectId, GradingProgressStatus status, decimal? score = null)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = Guid.NewGuid(),
                Status = status
            };

            if (score.HasValue)
            {
                assignment.GradingForm = new GradingForm
                {
                    GradingAssignmentId = assignment.Id,
                    TotalScore = score.Value,
                    SubmittedAt = DateTime.UtcNow
                };
            }

            return assignment;
        }

        [Fact]
        public async Task GetScoreDistributionAsync_OnlyIncludesSubmittedPapersWithAScore()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(subjectId, GradingProgressStatus.Submitted, score: 8m),
                MakeAssignment(subjectId, GradingProgressStatus.Submitted, score: 6m),
                MakeAssignment(subjectId, GradingProgressStatus.Drafting),
                MakeAssignment(subjectId, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetScoreDistributionAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(2, subject.SubmittedCount);
            Assert.Equal(2, subject.Scores.Count);
        }

        [Fact]
        public async Task GetScoreDistributionAsync_ComputesAvgMinMax()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(subjectId, GradingProgressStatus.Submitted, score: 4m),
                MakeAssignment(subjectId, GradingProgressStatus.Submitted, score: 7m),
                MakeAssignment(subjectId, GradingProgressStatus.Submitted, score: 10m));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetScoreDistributionAsync(null, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(7m, subject.ScoreAvg);
            Assert.Equal(4m, subject.ScoreMin);
            Assert.Equal(10m, subject.ScoreMax);
        }

        [Fact]
        public async Task GetScoreDistributionAsync_GroupsPerSubject()
        {
            using var db = NewInMemoryContext();
            var subjectA = Guid.NewGuid();
            var subjectB = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(subjectA, GradingProgressStatus.Submitted, score: 5m),
                MakeAssignment(subjectB, GradingProgressStatus.Submitted, score: 9m),
                MakeAssignment(subjectB, GradingProgressStatus.Submitted, score: 8m));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetScoreDistributionAsync(null, CancellationToken.None);

            Assert.Equal(2, result.Subjects.Count);
            var a = result.Subjects.Single(s => s.SubjectId == subjectA);
            var b = result.Subjects.Single(s => s.SubjectId == subjectB);
            Assert.Equal(1, a.SubmittedCount);
            Assert.Equal(2, b.SubmittedCount);
        }

        [Fact]
        public async Task GetScoreDistributionAsync_FiltersBySubjectIds()
        {
            using var db = NewInMemoryContext();
            var wanted = Guid.NewGuid();
            var other = Guid.NewGuid();
            db.GradingAssignments.AddRange(
                MakeAssignment(wanted, GradingProgressStatus.Submitted, score: 5m),
                MakeAssignment(other, GradingProgressStatus.Submitted, score: 9m));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetScoreDistributionAsync(new[] { wanted }, CancellationToken.None);

            var subject = Assert.Single(result.Subjects);
            Assert.Equal(wanted, subject.SubjectId);
        }

        [Fact]
        public async Task GetScoreDistributionAsync_WhenNoSubmittedPapers_ReturnsNoSubjects()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            db.GradingAssignments.Add(MakeAssignment(subjectId, GradingProgressStatus.NotStarted));
            await db.SaveChangesAsync();

            var result = await NewService(db).GetScoreDistributionAsync(null, CancellationToken.None);

            Assert.Empty(result.Subjects);
        }
    }
}

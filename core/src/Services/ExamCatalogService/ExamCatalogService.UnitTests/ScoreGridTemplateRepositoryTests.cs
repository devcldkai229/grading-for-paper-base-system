using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Infrastructure.Persistence;
using ExamCatalogService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class ScoreGridTemplateRepositoryTests
    {
        private static ExamCatalogDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ExamCatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ExamCatalogDbContext(options);
        }

        private static List<QuestionInputDto> MakeQuestions() => new()
        {
            new QuestionInputDto("Request 1 (20%)", "1", "Intro", 2m, 0),
            new QuestionInputDto("Request 2 (80%)", "2", "Main", 8m, 1),
        };

        [Fact]
        public async Task CreateAsync_PersistsTemplateAndReturnsSummary()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);
            var createdBy = Guid.NewGuid();
            var questions = MakeQuestions();

            var summary = await repo.CreateAsync("  PRN232 Standard  ", createdBy, questions, CancellationToken.None);

            Assert.Equal("PRN232 Standard", summary.Name); // trimmed
            Assert.Equal(createdBy, summary.CreatedBy);
            Assert.Equal(2, summary.QuestionCount);
            Assert.Single(db.ScoreGridTemplates.AsNoTracking());
        }

        [Fact]
        public async Task ListAsync_ReturnsSummariesOrderedByCreatedAtDescending()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);

            var first = await repo.CreateAsync("First", Guid.NewGuid(), MakeQuestions(), CancellationToken.None);
            // Force a distinct CreatedAt ordering since both could land in the same tick otherwise.
            var firstEntity = await db.ScoreGridTemplates.FirstAsync(t => t.Id == first.Id);
            db.Entry(firstEntity).Property("CreatedAt").CurrentValue = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();

            var second = await repo.CreateAsync("Second", Guid.NewGuid(), MakeQuestions(), CancellationToken.None);

            var list = await repo.ListAsync(CancellationToken.None);

            Assert.Equal(2, list.Count);
            Assert.Equal(second.Id, list[0].Id);
            Assert.Equal(first.Id, list[1].Id);
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);

            var result = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByIdAsync_WhenFound_ReturnsQuestionsMatchingWhatWasCreated()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);
            var questions = MakeQuestions();
            var created = await repo.CreateAsync("PRN232 Standard", Guid.NewGuid(), questions, CancellationToken.None);

            var detail = await repo.GetByIdAsync(created.Id, CancellationToken.None);

            Assert.NotNull(detail);
            Assert.Equal("PRN232 Standard", detail!.Name);
            Assert.Equal(2, detail.Questions.Count);
            Assert.Equal("Request 1 (20%)", detail.Questions[0].GroupLabel);
            Assert.Equal("1", detail.Questions[0].QuestionNumber);
            Assert.Equal(2m, detail.Questions[0].MaxScore);
            Assert.Equal("Request 2 (80%)", detail.Questions[1].GroupLabel);
            Assert.Equal(8m, detail.Questions[1].MaxScore);
        }

        [Fact]
        public async Task DeleteAsync_WhenFound_RemovesTemplateAndReturnsTrue()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);
            var created = await repo.CreateAsync("Disposable", Guid.NewGuid(), MakeQuestions(), CancellationToken.None);

            var deleted = await repo.DeleteAsync(created.Id, CancellationToken.None);

            Assert.True(deleted);
            Assert.Empty(db.ScoreGridTemplates.AsNoTracking());
        }

        [Fact]
        public async Task DeleteAsync_WhenNotFound_ReturnsFalse()
        {
            using var db = NewInMemoryContext();
            var repo = new ScoreGridTemplateRepository(db);

            var deleted = await repo.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.False(deleted);
        }
    }
}

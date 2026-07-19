using System;
using System.Threading;
using System.Threading.Tasks;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Persistence;
using ExamCatalogService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class SubjectGradingDeadlineRepositoryTests
    {
        private static ExamCatalogDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ExamCatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ExamCatalogDbContext(options);
        }

        private static Exam SeedExam(ExamCatalogDbContext db, DateOnly? examEndDate)
        {
            var semester = new Semester
            {
                Code = "SU26",
                Name = "SU26",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 6, 1),
                IsActive = true
            };
            var exam = new Exam
            {
                SemesterId = semester.Id,
                Name = "Final Exam",
                ExamType = ExamType.FE,
                EndDate = examEndDate,
                Semester = semester
            };
            db.Semesters.Add(semester);
            db.Exams.Add(exam);
            db.SaveChanges();
            return exam;
        }

        [Fact]
        public async Task CreateSubjectAsync_PersistsGradingDeadline()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, null);
            var repo = new ExamCatalogRepository(db);
            var deadline = new DateOnly(2026, 7, 27);

            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10, GradingDeadline: deadline),
                CancellationToken.None);

            Assert.Equal(deadline, created.GradingDeadline);
        }

        [Fact]
        public async Task UpdateSubjectAsync_UpdatesGradingDeadline()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, null);
            var repo = new ExamCatalogRepository(db);
            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10), CancellationToken.None);
            var newDeadline = new DateOnly(2026, 8, 1);

            var updated = await repo.UpdateSubjectAsync(
                created.Id,
                new UpdateSubjectRequest("PRN232", "Title", 10, GradingDeadline: newDeadline),
                CancellationToken.None);

            Assert.Equal(newDeadline, updated!.GradingDeadline);
        }

        [Fact]
        public async Task UpdateSubjectAsync_CanClearGradingDeadlineBackToNull()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, null);
            var repo = new ExamCatalogRepository(db);
            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10, GradingDeadline: new DateOnly(2026, 7, 27)),
                CancellationToken.None);

            var updated = await repo.UpdateSubjectAsync(
                created.Id,
                new UpdateSubjectRequest("PRN232", "Title", 10, GradingDeadline: null),
                CancellationToken.None);

            Assert.Null(updated!.GradingDeadline);
        }

        [Fact]
        public async Task GetSubjectDetailAsync_SurfacesGradingDeadline()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, null);
            var repo = new ExamCatalogRepository(db);
            var deadline = new DateOnly(2026, 7, 27);
            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10, GradingDeadline: deadline),
                CancellationToken.None);

            var detail = await repo.GetSubjectDetailAsync(created.Id, CancellationToken.None);

            Assert.Equal(deadline, detail!.GradingDeadline);
        }

        [Fact]
        public async Task SearchSubjectsAsync_SurfacesGradingDeadline()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, null);
            var repo = new ExamCatalogRepository(db);
            var deadline = new DateOnly(2026, 7, 27);
            await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10, GradingDeadline: deadline),
                CancellationToken.None);

            var (items, _) = await repo.SearchSubjectsAsync(
                null, null, null, null, null, 1, 20, CancellationToken.None);

            Assert.Equal(deadline, Assert.Single(items).GradingDeadline);
        }

        [Fact]
        public async Task GetSubjectExamInfoAsync_SurfacesGradingDeadlineAlongsideExamEndDate()
        {
            using var db = NewInMemoryContext();
            var examEndDate = new DateOnly(2026, 8, 15);
            var exam = SeedExam(db, examEndDate);
            var repo = new ExamCatalogRepository(db);
            var gradingDeadline = new DateOnly(2026, 7, 27);
            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10, GradingDeadline: gradingDeadline),
                CancellationToken.None);

            var info = await repo.GetSubjectExamInfoAsync(created.Id, CancellationToken.None);

            Assert.Equal(examEndDate, info!.ExamEndDate);
            Assert.Equal(gradingDeadline, info.GradingDeadline);
        }

        [Fact]
        public async Task GetSubjectExamInfoAsync_WhenGradingDeadlineNotSet_IsNull()
        {
            using var db = NewInMemoryContext();
            var exam = SeedExam(db, new DateOnly(2026, 8, 15));
            var repo = new ExamCatalogRepository(db);
            var created = await repo.CreateSubjectAsync(
                new CreateSubjectRequest(exam.Id, "PRN232", "Title", 10), CancellationToken.None);

            var info = await repo.GetSubjectExamInfoAsync(created.Id, CancellationToken.None);

            Assert.Null(info!.GradingDeadline);
        }
    }
}

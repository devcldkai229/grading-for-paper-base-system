using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Persistence;
using ExamCatalogService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class SubjectSearchRepositoryTests
    {
        private static ExamCatalogDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ExamCatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ExamCatalogDbContext(options);
        }

        private static (Semester semester, Exam exam) SeedExam(
            ExamCatalogDbContext db, string semesterCode, string examName)
        {
            var semester = new Semester
            {
                Code = semesterCode,
                Name = semesterCode,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 6, 1),
                IsActive = true
            };
            var exam = new Exam
            {
                SemesterId = semester.Id,
                Name = examName,
                ExamType = ExamType.FE,
                Semester = semester
            };
            db.Semesters.Add(semester);
            db.Exams.Add(exam);
            return (semester, exam);
        }

        private static Subject SeedSubject(
            ExamCatalogDbContext db, Exam exam, string code, SubjectStatus status)
        {
            var subject = new Subject
            {
                ExamId = exam.Id,
                Exam = exam,
                SubjectCode = code,
                MaxScore = 10,
                Status = status
            };
            db.Subjects.Add(subject);
            return subject;
        }

        [Fact]
        public async Task SearchSubjectsAsync_FiltersByCodeCaseInsensitiveSubstring()
        {
            using var db = NewInMemoryContext();
            var (_, exam) = SeedExam(db, "SP26", "FE Exam");
            SeedSubject(db, exam, "PRN222", SubjectStatus.Open);
            SeedSubject(db, exam, "PRN232", SubjectStatus.Open);
            SeedSubject(db, exam, "SWP391", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                "prn2", null, null, null, null, 1, 20);

            Assert.Equal(2, total);
            Assert.All(items, i => Assert.Contains("PRN2", i.SubjectCode));
        }

        [Fact]
        public async Task SearchSubjectsAsync_FiltersByStatus()
        {
            using var db = NewInMemoryContext();
            var (_, exam) = SeedExam(db, "SP26", "FE Exam");
            SeedSubject(db, exam, "PRN222", SubjectStatus.Draft);
            SeedSubject(db, exam, "PRN232", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                null, null, null, SubjectStatus.Open, null, 1, 20);

            Assert.Equal(1, total);
            Assert.Equal("PRN232", items[0].SubjectCode);
        }

        [Fact]
        public async Task SearchSubjectsAsync_FiltersBySemesterAcrossMultipleExams()
        {
            using var db = NewInMemoryContext();
            var (semesterA, examA) = SeedExam(db, "SP26", "FE Exam A");
            var (semesterB, examB) = SeedExam(db, "SU26", "FE Exam B");
            SeedSubject(db, examA, "PRN222", SubjectStatus.Open);
            SeedSubject(db, examB, "PRN232", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                null, semesterA.Id, null, null, null, 1, 20);

            Assert.Equal(1, total);
            Assert.Equal("PRN222", items[0].SubjectCode);
            Assert.Equal(semesterA.Id, items[0].SemesterId);
        }

        [Fact]
        public async Task SearchSubjectsAsync_FiltersByExamId()
        {
            using var db = NewInMemoryContext();
            var (_, examA) = SeedExam(db, "SP26", "FE Exam A");
            var (_, examB) = SeedExam(db, "SP26", "PE Exam B");
            SeedSubject(db, examA, "PRN222", SubjectStatus.Open);
            SeedSubject(db, examB, "PRN232", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                null, null, examB.Id, null, null, 1, 20);

            Assert.Equal(1, total);
            Assert.Equal("PRN232", items[0].SubjectCode);
        }

        [Fact]
        public async Task SearchSubjectsAsync_RestrictToSubjectIds_OnlyReturnsThoseSubjects()
        {
            using var db = NewInMemoryContext();
            var (_, exam) = SeedExam(db, "SP26", "FE Exam");
            var s1 = SeedSubject(db, exam, "PRN222", SubjectStatus.Open);
            SeedSubject(db, exam, "PRN232", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                null, null, null, null, new HashSet<Guid> { s1.Id }, 1, 20);

            Assert.Equal(1, total);
            Assert.Equal("PRN222", items[0].SubjectCode);
        }

        [Fact]
        public async Task SearchSubjectsAsync_RestrictToEmptySet_ReturnsNoResults()
        {
            using var db = NewInMemoryContext();
            var (_, exam) = SeedExam(db, "SP26", "FE Exam");
            SeedSubject(db, exam, "PRN222", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (items, total) = await repo.SearchSubjectsAsync(
                null, null, null, null, new HashSet<Guid>(), 1, 20);

            Assert.Equal(0, total);
            Assert.Empty(items);
        }

        [Fact]
        public async Task SearchSubjectsAsync_PaginatesOrderedByCode()
        {
            using var db = NewInMemoryContext();
            var (_, exam) = SeedExam(db, "SP26", "FE Exam");
            SeedSubject(db, exam, "C_SUBJECT", SubjectStatus.Open);
            SeedSubject(db, exam, "A_SUBJECT", SubjectStatus.Open);
            SeedSubject(db, exam, "B_SUBJECT", SubjectStatus.Open);
            await db.SaveChangesAsync();

            var repo = new ExamCatalogRepository(db);
            var (page1, total) = await repo.SearchSubjectsAsync(
                null, null, null, null, null, 1, 2);
            var (page2, _) = await repo.SearchSubjectsAsync(
                null, null, null, null, null, 2, 2);

            Assert.Equal(3, total);
            Assert.Equal(2, page1.Count);
            Assert.Equal("A_SUBJECT", page1[0].SubjectCode);
            Assert.Equal("B_SUBJECT", page1[1].SubjectCode);
            Assert.Single(page2);
            Assert.Equal("C_SUBJECT", page2[0].SubjectCode);
        }
    }
}

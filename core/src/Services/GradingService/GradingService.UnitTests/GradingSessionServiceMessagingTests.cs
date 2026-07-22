using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contracts.Messages;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Application.Services;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Files;
using GradingService.Infrastructure.Persistence;
using GradingService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace GradingService.UnitTests
{
    public class GradingSessionServiceMessagingTests
    {
        private static GradingDbContext NewInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<GradingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new GradingDbContext(options);
        }

        private static IExamCatalogServiceClient StubCatalog()
        {
            var catalog = Substitute.For<IExamCatalogServiceClient>();
            catalog.GetGradingGridAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new SubjectGradingGridClientDto(
                    Guid.NewGuid(), 10m, 1,
                    new List<SubjectQuestionClientDto> { new("1", null, "Q1", 10m, 0) },
                    "Open"));
            return catalog;
        }

        private static ISubmissionServiceClient StubSubmission()
        {
            var client = Substitute.For<ISubmissionServiceClient>();
            client.ListPapersByAliasRangeAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<InternalPaperSummaryClientDto>());
            client.MarkPapersAssignedAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(0);
            return client;
        }

        private static GradingSessionService NewService(
            GradingDbContext db, IMessagePublisher publisher,
            ISubmissionServiceClient? submissionClient = null,
            IExamCatalogServiceClient? catalogClient = null) => new(
            new GradingAssignmentRepository(db),
            new AuditLogRepository(db),
            new GradingResumePointerRepository(db),
            new GradingUnitOfWork(db),
            submissionClient ?? StubSubmission(),
            catalogClient ?? StubCatalog(),
            new MiniExcelGradeExportFileBuilder(),
            new MarkerAssignmentRepository(db),
            publisher);

        private static GradingAssignment SeedAssignment(
            GradingDbContext db, Guid teacherId, Guid subjectId, GradingProgressStatus status)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = status
            };
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static IExamCatalogServiceClient StubExamInfo(
            Guid subjectId, DateOnly? examEndDate, string subjectCode = "PRN232", DateOnly? gradingDeadline = null)
        {
            var client = Substitute.For<IExamCatalogServiceClient>();
            client.GetExamInfoAsync(subjectId, Arg.Any<CancellationToken>())
                .Returns(examEndDate is null && gradingDeadline is null
                    ? null
                    : new SubjectExamInfoClientDto(subjectId, subjectCode, Guid.NewGuid(), "Final Exam", examEndDate, gradingDeadline));
            return client;
        }

        private static GradingAssignment SeedSubmittedAssignment(GradingDbContext db, Guid teacherId, Guid subjectId, decimal score = 5m)
        {
            var assignment = new GradingAssignment
            {
                StudentPaperId = Guid.NewGuid(),
                SubjectId = subjectId,
                TeacherId = teacherId,
                Status = GradingProgressStatus.Submitted
            };

            var form = new GradingForm
            {
                GradingAssignmentId = assignment.Id,
                TotalScore = score,
                RowVersion = 1,
                SubmittedAt = DateTime.UtcNow
            };
            form.QuestionGradeDetails.Add(new QuestionGradeDetail
            {
                GradingFormId = form.Id,
                QuestionNumber = "Q1",
                OrderIndex = 0,
                Score = score,
                MaxScore = 10m
            });

            assignment.GradingForm = form;
            db.GradingAssignments.Add(assignment);
            db.SaveChanges();
            return assignment;
        }

        private static OverrideMarksRequest MakeOverrideRequest(decimal score, string reason) => new(
            new System.Collections.Generic.List<QuestionMarkInput> { new("Q1", score, "corrected") },
            "updated paper comment", "internal note", reason);

        [Fact]
        public async Task CreateMarkerAssignmentAsync_PublishesAssignmentNotificationEventForNewTeacher()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            await service.CreateMarkerAssignmentAsync(
                subjectId, new CreateMarkerAssignmentRequest(teacherId, 1, 20, null),
                Guid.NewGuid(), CancellationToken.None);

            await publisher.Received(1).PublishAsync(
                Arg.Is<AssignmentNotificationEvent>(e =>
                    e.TeacherId == teacherId && e.SubjectId == subjectId && e.AliasStart == 1 && e.AliasEnd == 20),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CreateMarkerAssignmentAsync_WhenValidationFails_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);

            await service.CreateMarkerAssignmentAsync(
                Guid.NewGuid(), new CreateMarkerAssignmentRequest(Guid.NewGuid(), null, null, null),
                Guid.NewGuid(), CancellationToken.None);

            await publisher.DidNotReceive().PublishAsync(
                Arg.Any<AssignmentNotificationEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ReassignMarkerAssignmentAsync_PublishesAssignmentNotificationEventForNewTeacher()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);
            var subjectId = Guid.NewGuid();
            var newTeacherId = Guid.NewGuid();

            var assignment = new MarkerAssignment
            {
                SubjectId = subjectId,
                TeacherId = Guid.NewGuid(),
                AliasStart = 1,
                AliasEnd = 20,
                AssignedBy = Guid.NewGuid()
            };
            db.MarkerAssignments.Add(assignment);
            db.SaveChanges();

            await service.ReassignMarkerAssignmentAsync(
                assignment.Id, new ReassignMarkerAssignmentRequest(newTeacherId, 1, 25),
                Guid.NewGuid(), CancellationToken.None);

            await publisher.Received(1).PublishAsync(
                Arg.Is<AssignmentNotificationEvent>(e =>
                    e.TeacherId == newTeacherId && e.SubjectId == subjectId && e.AliasEnd == 25),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task OverrideMarksAsync_WhenAdminOverridesAnotherTeachersAssignment_PublishesRegradeNotificationEvent()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);
            var teacherId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId, subjectId);

            await service.OverrideMarksAsync(
                assignment.Id, adminId, isAdmin: true, MakeOverrideRequest(9m, "student appealed"), CancellationToken.None);

            await publisher.Received(1).PublishAsync(
                Arg.Is<RegradeNotificationEvent>(e =>
                    e.TeacherId == teacherId && e.AssignmentId == assignment.Id &&
                    e.SubjectId == subjectId && e.Reason == "student appealed"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task OverrideMarksAsync_WhenTeacherOverridesTheirOwnAssignment_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);
            var teacherId = Guid.NewGuid();
            var assignment = SeedSubmittedAssignment(db, teacherId, Guid.NewGuid());

            await service.OverrideMarksAsync(
                assignment.Id, teacherId, isAdmin: false, MakeOverrideRequest(7m, "re-checked my own grading"), CancellationToken.None);

            await publisher.DidNotReceive().PublishAsync(
                Arg.Any<RegradeNotificationEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExportGradesAsync_WhenGradesExist_PublishesExportReadyEvent()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);
            var subjectId = Guid.NewGuid();
            var requestedBy = Guid.NewGuid();
            SeedSubmittedAssignment(db, Guid.NewGuid(), subjectId);

            var bytes = await service.ExportGradesAsync(subjectId, requestedBy, CancellationToken.None);

            Assert.NotNull(bytes);
            await publisher.Received(1).PublishAsync(
                Arg.Is<ExportReadyEvent>(e => e.RequestedBy == requestedBy && e.SubjectId == subjectId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExportGradesAsync_WhenSubjectHasNoGrades_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var service = NewService(db, publisher);

            var bytes = await service.ExportGradesAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            Assert.Null(bytes);
            await publisher.DidNotReceive().PublishAsync(
                Arg.Any<ExportReadyEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenDeadlineWithinWindowAndPapersRemaining_PublishesReminder()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var examEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
            var catalogClient = StubExamInfo(subjectId, examEndDate);
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.Submitted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(1, published);
            await publisher.Received(1).PublishAsync(
                Arg.Is<DeadlineReminderEvent>(e =>
                    e.TeacherId == teacherId && e.SubjectId == subjectId &&
                    e.RemainingCount == 1 && e.Deadline == examEndDate),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenDeadlineBeyondWindow_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var farFutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
            var catalogClient = StubExamInfo(subjectId, farFutureDate);
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(0, published);
            await publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineReminderEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenAllPapersSubmitted_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var catalogClient = StubExamInfo(subjectId, DateOnly.FromDateTime(DateTime.UtcNow));
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.Submitted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(0, published);
            await publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineReminderEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenExamInfoUnavailable_DoesNotPublish()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var catalogClient = StubExamInfo(subjectId, examEndDate: null);
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(0, published);
            await publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineReminderEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenGradingDeadlineSet_UsesItInsteadOfExamEndDate()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var farFutureExamEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
            var nearGradingDeadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
            var catalogClient = StubExamInfo(subjectId, farFutureExamEndDate, gradingDeadline: nearGradingDeadline);
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(1, published);
            await publisher.Received(1).PublishAsync(
                Arg.Is<DeadlineReminderEvent>(e => e.Deadline == nearGradingDeadline),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_WhenGradingDeadlineFarButExamEndDateNear_DoesNotUseExamEndDate()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var nearExamEndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
            var farGradingDeadline = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
            var catalogClient = StubExamInfo(subjectId, nearExamEndDate, gradingDeadline: farGradingDeadline);
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(0, published);
            await publisher.DidNotReceive().PublishAsync(Arg.Any<DeadlineReminderEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_IsIdempotentPerDay_SameMessageIdOnRepeatedRuns()
        {
            using var db = NewInMemoryContext();
            var subjectId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var examEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
            SeedAssignment(db, teacherId, subjectId, GradingProgressStatus.NotStarted);

            var firstPublisher = Substitute.For<IMessagePublisher>();
            var firstService = NewService(db, firstPublisher, catalogClient: StubExamInfo(subjectId, examEndDate));
            await firstService.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);
            var firstMessageId = ((DeadlineReminderEvent)firstPublisher.ReceivedCalls()
                .Single().GetArguments()[0]!).MessageId;

            var secondPublisher = Substitute.For<IMessagePublisher>();
            var secondService = NewService(db, secondPublisher, catalogClient: StubExamInfo(subjectId, examEndDate));
            await secondService.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);
            var secondMessageId = ((DeadlineReminderEvent)secondPublisher.ReceivedCalls()
                .Single().GetArguments()[0]!).MessageId;

            Assert.Equal(firstMessageId, secondMessageId);
        }

        [Fact]
        public async Task RunDeadlineReminderSweepAsync_PublishesSeparateReminderPerTeacher()
        {
            using var db = NewInMemoryContext();
            var publisher = Substitute.For<IMessagePublisher>();
            var subjectId = Guid.NewGuid();
            var teacherA = Guid.NewGuid();
            var teacherB = Guid.NewGuid();
            var catalogClient = StubExamInfo(subjectId, DateOnly.FromDateTime(DateTime.UtcNow));
            var service = NewService(db, publisher, catalogClient: catalogClient);
            SeedAssignment(db, teacherA, subjectId, GradingProgressStatus.NotStarted);
            SeedAssignment(db, teacherB, subjectId, GradingProgressStatus.NotStarted);

            var published = await service.RunDeadlineReminderSweepAsync(reminderWindowDays: 3, ct: CancellationToken.None);

            Assert.Equal(2, published);
            await publisher.Received(1).PublishAsync(
                Arg.Is<DeadlineReminderEvent>(e => e.TeacherId == teacherA), Arg.Any<CancellationToken>());
            await publisher.Received(1).PublishAsync(
                Arg.Is<DeadlineReminderEvent>(e => e.TeacherId == teacherB), Arg.Any<CancellationToken>());
        }
    }
}

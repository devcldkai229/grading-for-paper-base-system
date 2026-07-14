using System;
using System.Threading;
using System.Threading.Tasks;
using Contracts.Messages;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NSubstitute;
using Xunit;

namespace NotificationService.UnitTests
{
    public class NotificationDispatchServiceTests
    {
        private static (NotificationDispatchService Service, INotificationRepository Repository) NewService()
        {
            var repository = Substitute.For<INotificationRepository>();
            return (new NotificationDispatchService(repository), repository);
        }

        [Fact]
        public async Task HandleAssignmentAsync_WithValidTeacherId_PersistsSentNotification()
        {
            var (service, repository) = NewService();
            var teacherId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var evt = new AssignmentNotificationEvent(Guid.NewGuid(), teacherId, subjectId, 1, 20, DateTime.UtcNow);

            await service.HandleAssignmentAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.UserId == teacherId &&
                n.Type == NotificationType.Assignment &&
                n.Status == NotificationStatus.Sent &&
                n.SentAt != null &&
                n.Body!.Contains("Student_0001") &&
                n.Body!.Contains("Student_0020")), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAssignmentAsync_WithEmptyTeacherId_PersistsFailedNotification()
        {
            var (service, repository) = NewService();
            var evt = new AssignmentNotificationEvent(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 1, 20, DateTime.UtcNow);

            await service.HandleAssignmentAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.UserId == Guid.Empty &&
                n.Status == NotificationStatus.Failed &&
                n.SentAt == null), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleDeadlineReminderAsync_PersistsSentNotificationWithCorrectBody()
        {
            var (service, repository) = NewService();
            var teacherId = Guid.NewGuid();
            var examEndDate = new DateOnly(2026, 8, 1);
            var evt = new DeadlineReminderEvent(
                Guid.NewGuid(), teacherId, Guid.NewGuid(), "PRN232", examEndDate, 5, DateTime.UtcNow);

            await service.HandleDeadlineReminderAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.UserId == teacherId &&
                n.Type == NotificationType.DeadlineReminder &&
                n.Status == NotificationStatus.Sent &&
                n.Body!.Contains("PRN232") &&
                n.Body!.Contains("5") &&
                n.Body!.Contains("2026-08-01")), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleExportReadyAsync_PersistsSentNotificationForRequestedBy()
        {
            var (service, repository) = NewService();
            var requestedBy = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var evt = new ExportReadyEvent(Guid.NewGuid(), requestedBy, subjectId, DateTime.UtcNow);

            await service.HandleExportReadyAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.UserId == requestedBy &&
                n.Type == NotificationType.ExportReady &&
                n.Status == NotificationStatus.Sent &&
                n.Body!.Contains(subjectId.ToString())), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleRegradeAsync_PersistsSentNotificationWithReason()
        {
            var (service, repository) = NewService();
            var teacherId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            var evt = new RegradeNotificationEvent(
                Guid.NewGuid(), teacherId, assignmentId, Guid.NewGuid(), "student appealed", DateTime.UtcNow);

            await service.HandleRegradeAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.UserId == teacherId &&
                n.Type == NotificationType.RegradeRequest &&
                n.Status == NotificationStatus.Sent &&
                n.Body!.Contains("student appealed")), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleRegradeAsync_StoresRawEventAsMetadataForAudit()
        {
            var (service, repository) = NewService();
            var evt = new RegradeNotificationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "fix typo", DateTime.UtcNow);

            await service.HandleRegradeAsync(evt, CancellationToken.None);

            await repository.Received(1).AddAsync(Arg.Is<Notification>(n =>
                n.Metadata != null && n.Metadata.Contains(evt.AssignmentId.ToString())), Arg.Any<CancellationToken>());
        }
    }
}

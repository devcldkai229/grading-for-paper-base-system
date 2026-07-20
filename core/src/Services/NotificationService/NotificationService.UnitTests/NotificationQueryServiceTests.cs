using System;
using System.Collections.Generic;
using System.Text.Json;
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
    public class NotificationQueryServiceTests
    {
        private static (NotificationQueryService Service, INotificationRepository Repository) NewService()
        {
            var repository = Substitute.For<INotificationRepository>();
            return (new NotificationQueryService(repository), repository);
        }

        private static Notification MakeNotification(
            Guid userId, NotificationType type, string? metadata = null, bool isRead = false) => new()
        {
            UserId = userId,
            Type = type,
            Title = "Title",
            Body = "Body",
            Metadata = metadata,
            Status = NotificationStatus.Sent,
            SentAt = DateTime.UtcNow,
            IsRead = isRead,
        };

        [Fact]
        public async Task GetMyNotificationsAsync_MapsPagingFieldsFromRepository()
        {
            var (service, repository) = NewService();
            var userId = Guid.NewGuid();
            repository.ListByUserAsync(userId, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<Notification> { MakeNotification(userId, NotificationType.Assignment) }, 5));

            var result = await service.GetMyNotificationsAsync(userId, 1, 20, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(1, result.TotalPages);
            Assert.Equal(1, result.Page);
            Assert.Equal(20, result.PageSize);
        }

        [Fact]
        public async Task GetMyNotificationsAsync_ExtractsAssignmentIdForRegradeRequest()
        {
            var (service, repository) = NewService();
            var userId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            var evt = new RegradeNotificationEvent(
                Guid.NewGuid(), userId, assignmentId, Guid.NewGuid(), "student appealed", DateTime.UtcNow);
            repository.ListByUserAsync(userId, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<Notification>
                {
                    MakeNotification(userId, NotificationType.RegradeRequest, JsonSerializer.Serialize(evt))
                }, 1));

            var result = await service.GetMyNotificationsAsync(userId, 1, 20, CancellationToken.None);

            Assert.Equal(assignmentId, result.Items[0].AssignmentId);
        }

        [Fact]
        public async Task GetMyNotificationsAsync_OtherTypesHaveNoAssignmentId()
        {
            var (service, repository) = NewService();
            var userId = Guid.NewGuid();
            var evt = new AssignmentNotificationEvent(Guid.NewGuid(), userId, Guid.NewGuid(), 1, 20, DateTime.UtcNow);
            repository.ListByUserAsync(userId, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<Notification>
                {
                    MakeNotification(userId, NotificationType.Assignment, JsonSerializer.Serialize(evt))
                }, 1));

            var result = await service.GetMyNotificationsAsync(userId, 1, 20, CancellationToken.None);

            Assert.Null(result.Items[0].AssignmentId);
        }

        [Fact]
        public async Task GetMyNotificationsAsync_MalformedMetadata_DoesNotThrow()
        {
            var (service, repository) = NewService();
            var userId = Guid.NewGuid();
            repository.ListByUserAsync(userId, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<Notification>
                {
                    MakeNotification(userId, NotificationType.RegradeRequest, "not valid json")
                }, 1));

            var result = await service.GetMyNotificationsAsync(userId, 1, 20, CancellationToken.None);

            Assert.Null(result.Items[0].AssignmentId);
        }

        [Fact]
        public async Task GetMyNotificationsAsync_WhenNoNotifications_TotalPagesIsZero()
        {
            var (service, repository) = NewService();
            var userId = Guid.NewGuid();
            repository.ListByUserAsync(userId, 1, 20, Arg.Any<CancellationToken>())
                .Returns((new List<Notification>(), 0));

            var result = await service.GetMyNotificationsAsync(userId, 1, 20, CancellationToken.None);

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalPages);
        }

        [Fact]
        public async Task MarkAsReadAsync_DelegatesToRepository()
        {
            var (service, repository) = NewService();
            var notificationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            repository.MarkAsReadAsync(notificationId, userId, Arg.Any<CancellationToken>()).Returns(true);

            var result = await service.MarkAsReadAsync(notificationId, userId, CancellationToken.None);

            Assert.True(result);
            await repository.Received(1).MarkAsReadAsync(notificationId, userId, Arg.Any<CancellationToken>());
        }
    }
}

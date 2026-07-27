using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NotificationService.API;
using NotificationService.API.Controllers;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NSubstitute;
using Xunit;

namespace NotificationService.UnitTests
{
    public class NotificationsControllerTests
    {
        private readonly INotificationQueryService _notifications;
        private readonly NotificationsController _controller;
        private readonly Guid _userId;

        public NotificationsControllerTests()
        {
            _notifications = Substitute.For<INotificationQueryService>();
            _controller = new NotificationsController(_notifications);
            _userId = Guid.NewGuid();

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
            }, "TestAuth");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        [Fact]
        public async Task GetMyNotifications_ReturnsOkWithPageForCallingUser()
        {
            var page = new NotificationPageDto(Array.Empty<NotificationDto>(), 1, 20, 0, 0);
            _notifications.GetMyNotificationsAsync(_userId, 1, 20, Arg.Any<CancellationToken>()).Returns(page);

            var result = await _controller.GetMyNotifications(1, 20, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<NotificationPageDto>>(ok.Value);
            Assert.Same(page, body.Data);
        }

        [Fact]
        public async Task MarkAsRead_WhenFound_ReturnsOk()
        {
            var notificationId = Guid.NewGuid();
            _notifications.MarkAsReadAsync(notificationId, _userId, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _controller.MarkAsRead(notificationId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task MarkAsRead_WhenNotFound_ReturnsNotFound()
        {
            _notifications.MarkAsReadAsync(Arg.Any<Guid>(), _userId, Arg.Any<CancellationToken>()).Returns(false);

            var result = await _controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}

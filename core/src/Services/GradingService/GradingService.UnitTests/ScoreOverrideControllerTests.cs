using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GradingService.API;
using GradingService.API.Controllers;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace GradingService.UnitTests
{
    public class ScoreOverrideControllerTests
    {
        private readonly IGradingSessionService _sessionService;
        private readonly GradingController _controller;

        public ScoreOverrideControllerTests()
        {
            _sessionService = Substitute.For<IGradingSessionService>();
            _controller = new GradingController(_sessionService);
        }

        private void SetUser(Guid userId, string role)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("Role", role) // matches TokenService's actual claim Type, not ClaimTypes.Role
            }, "TestAuth");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        private static OverrideMarksRequest MakeRequest(string reason) => new(
            new List<QuestionMarkInput> { new("Q1", 8m, "ok") },
            "paper comment", "internal comment", reason);

        [Fact]
        public async Task OverrideMarks_WithBlankReason_ReturnsBadRequestWithoutCallingService()
        {
            SetUser(Guid.NewGuid(), "Admin");

            var result = await _controller.OverrideMarks(Guid.NewGuid(), MakeRequest("   "), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            await _sessionService.DidNotReceive().OverrideMarksAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task OverrideMarks_WhenServiceReportsNotFound_ReturnsNotFound()
        {
            var assignmentId = Guid.NewGuid();
            SetUser(Guid.NewGuid(), "Lecturer");
            _sessionService.OverrideMarksAsync(assignmentId, Arg.Any<Guid>(), false, Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>())
                .Returns(((OverrideMarksResultDto?)null, true, (string?)null));

            var result = await _controller.OverrideMarks(assignmentId, MakeRequest("fix typo"), CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task OverrideMarks_WhenServiceReportsValidationError_ReturnsBadRequest()
        {
            var assignmentId = Guid.NewGuid();
            SetUser(Guid.NewGuid(), "Admin");
            _sessionService.OverrideMarksAsync(assignmentId, Arg.Any<Guid>(), true, Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>())
                .Returns(((OverrideMarksResultDto?)null, false, "Score for question Q1 must be between 0 and 5."));

            var result = await _controller.OverrideMarks(assignmentId, MakeRequest("fix typo"), CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task OverrideMarks_WhenAdminAndSucceeds_ReturnsOkAndPassesIsAdminTrue()
        {
            var assignmentId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            SetUser(adminId, "Admin");
            _sessionService.OverrideMarksAsync(assignmentId, adminId, true, Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>())
                .Returns((new OverrideMarksResultDto(3, 8m), false, (string?)null));

            var result = await _controller.OverrideMarks(assignmentId, MakeRequest("student appealed"), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<OverrideMarksResultDto>>(ok.Value);
            Assert.Equal(200, body.StatusCode);
            Assert.Equal(8m, body.Data!.TotalScore);
        }

        [Fact]
        public async Task OverrideMarks_WhenLecturer_PassesIsAdminFalse()
        {
            var assignmentId = Guid.NewGuid();
            var lecturerId = Guid.NewGuid();
            SetUser(lecturerId, "Lecturer");
            _sessionService.OverrideMarksAsync(assignmentId, lecturerId, false, Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>())
                .Returns((new OverrideMarksResultDto(1, 8m), false, (string?)null));

            await _controller.OverrideMarks(assignmentId, MakeRequest("re-checked my own grading"), CancellationToken.None);

            await _sessionService.Received(1).OverrideMarksAsync(
                assignmentId, lecturerId, false, Arg.Any<OverrideMarksRequest>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetAuditLog_WhenNotFound_ReturnsNotFound()
        {
            var assignmentId = Guid.NewGuid();
            SetUser(Guid.NewGuid(), "Lecturer");
            _sessionService.GetAuditTrailAsync(assignmentId, Arg.Any<Guid>(), false, Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditLogEntryDto>?)null, true));

            var result = await _controller.GetAuditLog(assignmentId, CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetAuditLog_WhenFound_ReturnsOkWithEntries()
        {
            var assignmentId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            SetUser(adminId, "Admin");
            var entries = new List<AuditLogEntryDto>
            {
                new(Guid.NewGuid(), adminId, "ScoreOverridden", "{}", "{}", "student appealed", DateTime.UtcNow)
            };
            _sessionService.GetAuditTrailAsync(assignmentId, adminId, true, Arg.Any<CancellationToken>())
                .Returns(((IReadOnlyList<AuditLogEntryDto>?)entries, false));

            var result = await _controller.GetAuditLog(assignmentId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<IReadOnlyList<AuditLogEntryDto>>>(ok.Value);
            Assert.Single(body.Data!);
        }
    }
}

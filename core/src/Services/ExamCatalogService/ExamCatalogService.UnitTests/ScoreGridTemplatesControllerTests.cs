using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ExamCatalogService.API;
using ExamCatalogService.API.Controllers;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace ExamCatalogService.UnitTests
{
    public class ScoreGridTemplatesControllerTests
    {
        private readonly IScoreGridTemplateRepository _repository;
        private readonly ISubjectAdminService _subjectAdminService;
        private readonly ScoreGridTemplatesController _controller;

        public ScoreGridTemplatesControllerTests()
        {
            _repository = Substitute.For<IScoreGridTemplateRepository>();
            _subjectAdminService = Substitute.For<ISubjectAdminService>();
            _controller = new ScoreGridTemplatesController(_repository, _subjectAdminService);

            SetUser(Guid.NewGuid());
        }

        private void SetUser(Guid userId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("Role", "Admin")
            }, "TestAuth");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }

        private static List<QuestionInputDto> MakeQuestions() => new()
        {
            new QuestionInputDto(null, "1", null, 10m, 0),
        };

        [Fact]
        public async Task ListTemplates_ReturnsOkWithRepositoryData()
        {
            var templates = new List<ScoreGridTemplateSummaryDto>
            {
                new(Guid.NewGuid(), "Template A", Guid.NewGuid(), 3, DateTime.UtcNow)
            };
            _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(templates);

            var result = await _controller.ListTemplates(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<IReadOnlyList<ScoreGridTemplateSummaryDto>>>(ok.Value);
            Assert.Single(body.Data!);
        }

        [Fact]
        public async Task GetTemplate_WhenNotFound_ReturnsNotFound()
        {
            _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((ScoreGridTemplateDetailDto?)null);

            var result = await _controller.GetTemplate(Guid.NewGuid(), CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetTemplate_WhenFound_ReturnsOkWithDetail()
        {
            var templateId = Guid.NewGuid();
            var detail = new ScoreGridTemplateDetailDto(
                templateId, "Template A", Guid.NewGuid(), MakeQuestions(), DateTime.UtcNow);
            _repository.GetByIdAsync(templateId, Arg.Any<CancellationToken>()).Returns(detail);

            var result = await _controller.GetTemplate(templateId, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<ScoreGridTemplateDetailDto>>(ok.Value);
            Assert.Equal(templateId, body.Data!.Id);
        }

        [Fact]
        public async Task CreateTemplate_WithBlankName_ReturnsBadRequestWithoutValidatingOrSaving()
        {
            var request = new CreateScoreGridTemplateRequest("   ", MakeQuestions());

            var result = await _controller.CreateTemplate(request, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            _subjectAdminService.DidNotReceive().ValidateTemplateQuestions(Arg.Any<IReadOnlyList<QuestionInputDto>>());
            await _repository.DidNotReceive().CreateAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<QuestionInputDto>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CreateTemplate_WhenValidationFails_ReturnsBadRequestWithoutSaving()
        {
            var questions = MakeQuestions();
            _subjectAdminService.ValidateTemplateQuestions(questions)
                .Returns((new List<string> { "Question 1: maxScore must be greater than 0." }, new List<string>()));
            var request = new CreateScoreGridTemplateRequest("Template A", questions);

            var result = await _controller.CreateTemplate(request, CancellationToken.None);

            Assert.IsType<BadRequestObjectResult>(result);
            await _repository.DidNotReceive().CreateAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<QuestionInputDto>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CreateTemplate_WhenValid_SavesWithCreatedByFromClaimsAndReturnsOk()
        {
            var userId = Guid.NewGuid();
            SetUser(userId);
            var questions = MakeQuestions();
            _subjectAdminService.ValidateTemplateQuestions(questions)
                .Returns((new List<string>(), new List<string>()));
            var summary = new ScoreGridTemplateSummaryDto(Guid.NewGuid(), "Template A", userId, 1, DateTime.UtcNow);
            _repository.CreateAsync("Template A", userId, questions, Arg.Any<CancellationToken>())
                .Returns(summary);
            var request = new CreateScoreGridTemplateRequest("Template A", questions);

            var result = await _controller.CreateTemplate(request, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<ApiResponse<ScoreGridTemplateSummaryDto>>(ok.Value);
            Assert.Equal(userId, body.Data!.CreatedBy);
            await _repository.Received(1).CreateAsync("Template A", userId, questions, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteTemplate_WhenNotFound_ReturnsNotFound()
        {
            _repository.DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

            var result = await _controller.DeleteTemplate(Guid.NewGuid(), CancellationToken.None);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteTemplate_WhenFound_ReturnsOk()
        {
            var templateId = Guid.NewGuid();
            _repository.DeleteAsync(templateId, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _controller.DeleteTemplate(templateId, CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }
    }
}

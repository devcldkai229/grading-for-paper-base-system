using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SubmissionService.API.Controllers;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class InternalPapersControllerTests
    {
        private readonly IStudentPaperRepository _paperRepository;
        private readonly InternalPapersController _controller;

        public InternalPapersControllerTests()
        {
            _paperRepository = Substitute.For<IStudentPaperRepository>();
            _controller = new InternalPapersController(_paperRepository);
        }

        [Fact]
        public async Task GetPapersByIds_DelegatesToRepositoryWithRequestedIds()
        {
            var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var summaries = new List<InternalPaperSummaryDto>
            {
                new(ids[0], Guid.NewGuid(), Guid.NewGuid(), "Student_0001", 1, Guid.NewGuid())
            };
            _paperRepository.GetPapersByIdsAsync(ids, Arg.Any<CancellationToken>()).Returns(summaries);

            var result = await _controller.GetPapersByIds(new BatchPaperIdsRequest(ids), CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result);
            await _paperRepository.Received(1).GetPapersByIdsAsync(ids, Arg.Any<CancellationToken>());
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task GetPapersByIds_WhenNoneFound_ReturnsEmptyList()
        {
            var ids = new List<Guid> { Guid.NewGuid() };
            _paperRepository.GetPapersByIdsAsync(ids, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<InternalPaperSummaryDto>());

            var result = await _controller.GetPapersByIds(new BatchPaperIdsRequest(ids), CancellationToken.None);

            Assert.IsType<OkObjectResult>(result);
        }
    }
}

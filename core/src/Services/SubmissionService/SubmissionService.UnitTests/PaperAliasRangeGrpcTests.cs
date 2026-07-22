using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gradepaper.Submission.V1;
using Grpc.Core;
using NSubstitute;
using SubmissionService.API.Services.Grpc;
using SubmissionService.Application;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class PaperAliasRangeGrpcTests
    {
        private static PaperGrpcService NewService(IStudentPaperRepository? repository = null) =>
            new(
                repository ?? Substitute.For<IStudentPaperRepository>(),
                Substitute.For<IS3Service>(),
                new PresignedUrlOptions { Ttl = TimeSpan.FromMinutes(5) });

        [Fact]
        public async Task MarkPapersAssigned_ForwardsPaperIdsToRepository()
        {
            var repository = Substitute.For<IStudentPaperRepository>();
            var paperId = Guid.NewGuid();
            repository.MarkPapersAssignedAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(1);

            var service = NewService(repository);
            var request = new MarkPapersAssignedRequest();
            request.PaperIds.Add(paperId.ToString("D"));

            var response = await service.MarkPapersAssigned(request, new FakeServerCallContext());

            Assert.Equal(1, response.UpdatedCount);
            await repository.Received(1).MarkPapersAssignedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(paperId)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ListPapersByAliasRange_ForwardsSubjectAndAliasBoundsToRepository()
        {
            var repository = Substitute.For<IStudentPaperRepository>();
            var subjectId = Guid.NewGuid();
            repository.GetPapersByAliasRangeAsync(
                    subjectId, 3, 7, "ready_to_assign", Arg.Any<CancellationToken>())
                .Returns(new List<InternalPaperSummaryDto>
                {
                    new(Guid.NewGuid(), Guid.NewGuid(), subjectId, "Student_0003", 3, Guid.NewGuid())
                });

            var service = NewService(repository);
            var request = new AliasRangeRef
            {
                SubjectId = subjectId.ToString("D"),
                AliasStart = 3,
                AliasEnd = 7,
                StatusFilter = "ready_to_assign"
            };
            var writer = new FakeStreamWriter<PaperSummary>();

            await service.ListPapersByAliasRange(request, writer, new FakeServerCallContext());

            await repository.Received(1).GetPapersByAliasRangeAsync(
                subjectId, 3, 7, "ready_to_assign", Arg.Any<CancellationToken>());
            Assert.Single(writer.Items);
        }

        private sealed class FakeStreamWriter<T> : IServerStreamWriter<T>
        {
            public List<T> Items { get; } = new();
            public WriteOptions? WriteOptions { get; set; }

            public Task WriteAsync(T message) => WriteAsync(message, CancellationToken.None);

            public Task WriteAsync(T message, CancellationToken cancellationToken)
            {
                Items.Add(message);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeServerCallContext : ServerCallContext
        {
            protected override string MethodCore => "test";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "peer";
            protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
            protected override Metadata RequestHeadersCore => new();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore => new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions? WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
                Task.CompletedTask;
        }
    }
}

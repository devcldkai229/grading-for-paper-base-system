using Gradepaper.Submission.V1;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients.Grpc;

/// <summary>
/// gRPC implementation of <see cref="ISubmissionServiceClient"/> sitting behind the unchanged
/// Application seam. <see cref="GetBatchPapersAsync"/> re-assembles the server-streamed
/// header+papers frames into the existing <see cref="BatchPapersClientDto"/>. Preserves the
/// graceful-degrade convention: any <see cref="RpcException"/> returns <c>null</c>.
/// </summary>
public sealed class PaperGrpcClient : ISubmissionServiceClient
{
    private readonly PaperService.PaperServiceClient _client;
    private readonly ILogger<PaperGrpcClient> _logger;

    public PaperGrpcClient(PaperService.PaperServiceClient client, ILogger<PaperGrpcClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<BatchPapersClientDto?> GetBatchPapersAsync(Guid batchId, CancellationToken ct = default)
    {
        try
        {
            Guid resolvedBatchId = batchId;
            Guid? subjectId = null;
            Guid? uploadedBy = null;
            var papers = new List<BatchPaperClientDto>();

            using var call = _client.StreamBatchPapers(
                new BatchRef { BatchId = batchId.ToString("D") }, cancellationToken: ct);

            await foreach (var item in call.ResponseStream.ReadAllAsync(ct))
            {
                switch (item.ItemCase)
                {
                    case BatchPaperStreamItem.ItemOneofCase.Header:
                        resolvedBatchId = Guid.Parse(item.Header.BatchId);
                        subjectId = Guid.Parse(item.Header.SubjectId);
                        uploadedBy = Guid.Parse(item.Header.UploadedBy);
                        break;
                    case BatchPaperStreamItem.ItemOneofCase.Paper:
                        var p = item.Paper;
                        papers.Add(new BatchPaperClientDto(
                            Guid.Parse(p.PaperId),
                            Guid.Parse(p.SubjectId),
                            p.HasAliasNumber ? p.AliasNumber : null));
                        break;
                }
            }

            if (subjectId is null || uploadedBy is null)
            {
                return null;
            }

            return new BatchPapersClientDto(resolvedBatchId, subjectId.Value, uploadedBy.Value, papers);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC unreachable for batch {BatchId}", batchId);
            return null;
        }
    }

    public async Task<InternalPaperSummaryClientDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default)
    {
        try
        {
            var summary = await _client.GetPaperSummaryAsync(
                new PaperRef { PaperId = paperId.ToString("D") }, cancellationToken: ct);
            return ToDto(summary);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC unreachable for paper {PaperId}", paperId);
            return null;
        }
    }

    public async Task<SubjectPaperStatsClientDto?> GetSubjectPaperStatsAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var stats = await _client.GetSubjectPaperStatsAsync(
                new SubjectRef { SubjectId = subjectId.ToString("D") }, cancellationToken: ct);
            return new SubjectPaperStatsClientDto(
                stats.TotalPapers,
                stats.HasMaxAliasNumber ? stats.MaxAliasNumber : null);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC unreachable for subject {SubjectId} paper stats", subjectId);
            return null;
        }
    }

    public async Task<IReadOnlyList<AiGradeFileRef>?> GetPaperFileUrlsAsync(Guid paperId, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetPaperFileUrlsAsync(
                new PaperRef { PaperId = paperId.ToString("D") }, cancellationToken: ct);

            return response.Files
                .Select(f => new AiGradeFileRef(f.Url, f.ContentType))
                .ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC unreachable for paper {PaperId} files", paperId);
            return null;
        }
    }

    public async Task<IReadOnlyList<InternalPaperSummaryClientDto>?> GetPaperSummariesAsync(
        IReadOnlyCollection<Guid> paperIds, CancellationToken ct = default)
    {
        try
        {
            var request = new PaperRefs();
            request.PaperIds.AddRange(paperIds.Select(id => id.ToString("D")));

            var response = await _client.GetPaperSummariesAsync(request, cancellationToken: ct);
            return response.Papers.Select(ToDto).ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC unreachable for bulk paper summaries");
            return null;
        }
    }

    public async Task<bool> SetPapersAssignmentStatusAsync(
        IReadOnlyCollection<Guid> paperIds, bool assigned, CancellationToken ct = default)
    {
        try
        {
            var request = new SetPapersAssignmentStatusRequest { Assigned = assigned };
            request.PaperIds.AddRange(paperIds.Select(id => id.ToString("D")));
            await _client.SetPapersAssignmentStatusAsync(request, cancellationToken: ct);
            return true;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "SubmissionService gRPC failed to update assignment status");
            return false;
        }
    }

    private static InternalPaperSummaryClientDto ToDto(PaperSummary summary) => new(
        Guid.Parse(summary.Id),
        Guid.Parse(summary.BatchId),
        Guid.Parse(summary.SubjectId),
        summary.HasStudentAlias ? summary.StudentAlias : null,
        summary.HasAliasNumber ? summary.AliasNumber : null,
        Guid.Parse(summary.UploadedBy));
}

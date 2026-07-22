using Gradepaper.Submission.V1;
using Grpc.Core;
using SubmissionService.Application;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.API.Services.Grpc;

/// <summary>
/// gRPC surface for the Submission paper metadata + pre-signed file URLs that GradingService
/// consumes synchronously. Mirrors <c>InternalPapersController</c> behaviour, plus a
/// server-streaming <see cref="StreamBatchPapers"/> that walks a Mongo cursor (header frame first,
/// then one frame per paper) instead of materializing the whole batch. Guids travel as "D" strings;
/// nullable ints/strings use proto3 optional. A missing paper/batch surfaces as
/// <see cref="StatusCode.NotFound"/>.
/// </summary>
public sealed class PaperGrpcService : PaperService.PaperServiceBase
{
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly PresignedUrlOptions _presignedUrlOptions;

    public PaperGrpcService(
        IStudentPaperRepository paperRepository,
        IS3Service s3Service,
        PresignedUrlOptions presignedUrlOptions)
    {
        _paperRepository = paperRepository;
        _s3Service = s3Service;
        _presignedUrlOptions = presignedUrlOptions;
    }

    public override async Task StreamBatchPapers(
        BatchRef request,
        IServerStreamWriter<BatchPaperStreamItem> responseStream,
        ServerCallContext context)
    {
        var batchId = ParseGuid(request.BatchId, "batch_id");
        var ct = context.CancellationToken;

        var header = await _paperRepository.GetBatchSummaryAsync(batchId, ct);
        if (header is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Batch not found"));
        }

        await responseStream.WriteAsync(new BatchPaperStreamItem
        {
            Header = new BatchHeader
            {
                BatchId = header.BatchId.ToString("D"),
                SubjectId = header.SubjectId.ToString("D"),
                UploadedBy = header.UploadedBy.ToString("D")
            }
        });

        await foreach (var paper in _paperRepository.StreamPapersByBatchAsync(batchId, ct))
        {
            var item = new BatchPaperItem
            {
                PaperId = paper.PaperId.ToString("D"),
                SubjectId = paper.SubjectId.ToString("D")
            };
            if (paper.AliasNumber is { } alias) item.AliasNumber = alias;

            await responseStream.WriteAsync(new BatchPaperStreamItem { Paper = item });
        }
    }

    public override async Task<PaperSummary> GetPaperSummary(PaperRef request, ServerCallContext context)
    {
        var paperId = ParseGuid(request.PaperId, "paper_id");
        var summary = await _paperRepository.GetPaperSummaryAsync(paperId, context.CancellationToken);
        if (summary is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Paper not found"));
        }

        return ToProto(summary);
    }

    public override async Task<PaperSummaries> GetPaperSummaries(PaperRefs request, ServerCallContext context)
    {
        var paperIds = request.PaperIds
            .Select(id => ParseGuid(id, "paper_ids"))
            .ToList();

        var summaries = await _paperRepository.GetPapersByIdsAsync(paperIds, context.CancellationToken);

        var result = new PaperSummaries();
        result.Papers.AddRange(summaries.Select(ToProto));
        return result;
    }

    public override async Task<SubjectPaperStats> GetSubjectPaperStats(SubjectRef request, ServerCallContext context)
    {
        var subjectId = ParseGuid(request.SubjectId, "subject_id");
        var stats = await _paperRepository.GetSubjectPaperStatsAsync(subjectId, context.CancellationToken);

        var result = new SubjectPaperStats { TotalPapers = stats.TotalPapers };
        if (stats.MaxAliasNumber is { } max) result.MaxAliasNumber = max;
        return result;
    }

    public override async Task<PaperFileUrls> GetPaperFileUrls(PaperRef request, ServerCallContext context)
    {
        var paperId = ParseGuid(request.PaperId, "paper_id");
        var ct = context.CancellationToken;

        var detail = await _paperRepository.GetPaperDetailAsync(paperId, ct);
        if (detail is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Paper not found"));
        }

        var result = new PaperFileUrls();
        foreach (var file in detail.Files)
        {
            var info = await _paperRepository.GetPaperFileInfoAsync(paperId, file.Id, ct);
            if (info is null) continue;

            var (s3Key, fileName, contentType) = info.Value;
            var url = await _s3Service.GeneratePresignedGetUrlAsync(s3Key, _presignedUrlOptions.Ttl, ct);

            var proto = new PaperFileUrl { Url = url, ContentType = contentType };
            if (fileName is not null) proto.FileName = fileName;
            result.Files.Add(proto);
        }

        return result;
    }

    public override async Task<SetPapersAssignmentStatusReply> SetPapersAssignmentStatus(
        SetPapersAssignmentStatusRequest request, ServerCallContext context)
    {
        var paperIds = request.PaperIds.Select(id => ParseGuid(id, "paper_ids")).ToList();
        var updated = await _paperRepository.SetAssignmentStatusAsync(
            paperIds, request.Assigned, context.CancellationToken);
        return new SetPapersAssignmentStatusReply { UpdatedCount = updated };
    }

    private static PaperSummary ToProto(SubmissionService.Application.DTOs.InternalPaperSummaryDto summary)
    {
        var proto = new PaperSummary
        {
            Id = summary.Id.ToString("D"),
            BatchId = summary.BatchId.ToString("D"),
            SubjectId = summary.SubjectId.ToString("D"),
            UploadedBy = summary.UploadedBy.ToString("D")
        };
        if (summary.StudentAlias is not null) proto.StudentAlias = summary.StudentAlias;
        if (summary.AliasNumber is { } alias) proto.AliasNumber = alias;
        return proto;
    }

    private static Guid ParseGuid(string value, string fieldName)
    {
        if (!Guid.TryParse(value, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} is not a valid GUID"));
        }
        return id;
    }
}

using System.Globalization;
using ExamCatalogService.Application.Interfaces;
using Gradepaper.ExamCatalog.V1;
using Grpc.Core;

namespace ExamCatalogService.API.Services.Grpc;

/// <summary>
/// gRPC surface for the ExamCatalog rubric/grid/exam-info reads that GradingService consumes
/// synchronously. Mirrors <c>InternalSubjectsController</c> exactly (same repository calls, same
/// rubric-text rendering) so the REST and gRPC paths stay behaviourally identical during cutover.
/// Guid values travel as "D" strings and decimals as invariant-culture strings to preserve score
/// precision. A missing subject surfaces as <see cref="StatusCode.NotFound"/>.
/// </summary>
public sealed class RubricGrpcService : RubricService.RubricServiceBase
{
    // Presigned URLs must outlive the queued AI grading round-trip (download happens in the worker).
    private static readonly TimeSpan SourceFileTtl = TimeSpan.FromMinutes(30);

    private readonly IExamCatalogRepository _repository;
    private readonly IS3Service _s3Service;

    public RubricGrpcService(IExamCatalogRepository repository, IS3Service s3Service)
    {
        _repository = repository;
        _s3Service = s3Service;
    }

    public override async Task<GradingGrid> GetGradingGrid(SubjectRef request, ServerCallContext context)
    {
        var subjectId = ParseSubjectId(request.SubjectId);
        var subject = await _repository.GetSubjectDetailAsync(subjectId, context.CancellationToken);
        if (subject is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Subject not found"));
        }

        var grid = new GradingGrid
        {
            SubjectId = subject.Id.ToString("D"),
            MaxScore = subject.MaxScore.ToString(CultureInfo.InvariantCulture),
            RubricVersion = subject.RubricVersion,
            Status = subject.Status
        };

        foreach (var q in subject.Questions.OrderBy(q => q.OrderIndex))
        {
            var question = new GradingGridQuestion
            {
                QuestionNumber = q.QuestionNumber,
                MaxScore = q.MaxScore.ToString(CultureInfo.InvariantCulture),
                OrderIndex = q.OrderIndex
            };
            if (q.GroupLabel is not null) question.GroupLabel = q.GroupLabel;
            if (q.Label is not null) question.Label = q.Label;
            grid.Questions.Add(question);
        }

        return grid;
    }

    public override async Task<ExamInfo> GetExamInfo(SubjectRef request, ServerCallContext context)
    {
        var subjectId = ParseSubjectId(request.SubjectId);
        var info = await _repository.GetSubjectExamInfoAsync(subjectId, context.CancellationToken);
        if (info is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Subject not found"));
        }

        var result = new ExamInfo
        {
            SubjectId = info.SubjectId.ToString("D"),
            SubjectCode = info.SubjectCode,
            ExamId = info.ExamId.ToString("D"),
            ExamName = info.ExamName
        };
        if (info.ExamEndDate is { } examEnd) result.ExamEndDate = FormatDate(examEnd);
        if (info.GradingDeadline is { } deadline) result.GradingDeadline = FormatDate(deadline);

        return result;
    }

    public override async Task<RubricText> GetRubricText(SubjectRef request, ServerCallContext context)
    {
        var subjectId = ParseSubjectId(request.SubjectId);
        var subject = await _repository.GetSubjectDetailAsync(subjectId, context.CancellationToken);
        if (subject is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Subject not found"));
        }

        var lines = subject.Questions
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.QuestionNumber)
            .Select(q =>
            {
                var group = string.IsNullOrWhiteSpace(q.GroupLabel) ? "" : $" [{q.GroupLabel}]";
                var label = string.IsNullOrWhiteSpace(q.Label) ? "" : $" — {q.Label}";
                return $"{q.QuestionNumber}{group}{label} (max {q.MaxScore})";
            });

        return new RubricText { Text = string.Join('\n', lines) };
    }

    public override async Task<RubricSourceFiles> GetRubricSourceFiles(SubjectRef request, ServerCallContext context)
    {
        var subjectId = ParseSubjectId(request.SubjectId);
        var ct = context.CancellationToken;

        var result = new RubricSourceFiles();

        // Original (non-preview) rubric file — the real barem the AI should read.
        var rubricInfo = await _repository.GetRubricOriginalInfoAsync(subjectId, ct);
        if (rubricInfo is { } rubric && !string.IsNullOrWhiteSpace(rubric.S3Key))
        {
            var url = await _s3Service.GeneratePresignedGetUrlAsync(
                rubric.S3Key, SourceFileTtl, rubric.FileName, inline: true, ct);
            result.Files.Add(new RubricSourceFile
            {
                Url = url,
                ContentType = rubric.ContentType,
                Kind = "rubric"
            });
        }

        // Exam paper (optional) — extra context for questions that reference it.
        var examInfo = await _repository.GetExamPaperOriginalInfoAsync(subjectId, ct);
        if (examInfo is { } exam && !string.IsNullOrWhiteSpace(exam.S3Key))
        {
            var url = await _s3Service.GeneratePresignedGetUrlAsync(
                exam.S3Key, SourceFileTtl, exam.FileName, inline: true, ct);
            result.Files.Add(new RubricSourceFile
            {
                Url = url,
                ContentType = exam.ContentType,
                Kind = "exam_paper"
            });
        }

        return result;
    }

    public override async Task<CompiledRubric> GetCompiledRubric(CompiledRubricRef request, ServerCallContext context)
    {
        var subjectId = ParseSubjectId(request.SubjectId);
        var ct = context.CancellationToken;

        var contract = await _repository.GetApprovedGradingContractAsync(
            subjectId, request.HasRubricVersion ? request.RubricVersion : null, ct);
        if (contract is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound, "No approved compiled rubric for this subject/version"));
        }

        var result = new CompiledRubric
        {
            SubjectId = subjectId.ToString("D"),
            RubricVersion = contract.RubricVersion,
            Status = contract.Status.ToString(),
            ContractJson = contract.ContractJson,
            CoverageOk = contract.CoverageOk
        };

        var assets = await _repository.GetRubricAssetsAsync(subjectId, contract.RubricVersion, ct);
        foreach (var asset in assets)
        {
            var url = await _s3Service.GeneratePresignedGetUrlAsync(
                asset.S3Key, SourceFileTtl, inline: true, ct: ct);
            var grpcAsset = new CompiledRubricAsset
            {
                AssetId = asset.AssetId,
                Url = url,
                ContentType = asset.ContentType
            };
            if (asset.Question is not null) grpcAsset.Question = asset.Question;
            result.Assets.Add(grpcAsset);
        }

        return result;
    }

    private static Guid ParseSubjectId(string value)
    {
        if (!Guid.TryParse(value, out var id))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "subject_id is not a valid GUID"));
        }
        return id;
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

using System.Globalization;
using Gradepaper.ExamCatalog.V1;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients.Grpc;

/// <summary>
/// gRPC implementation of <see cref="IExamCatalogServiceClient"/> sitting behind the unchanged
/// Application seam. Maps the proto contract back to the existing App DTOs and preserves the
/// graceful-degrade convention: any <see cref="RpcException"/> (subject missing, service
/// unavailable, deadline exceeded) returns <c>null</c> rather than propagating.
/// </summary>
public sealed class ExamCatalogGrpcClient : IExamCatalogServiceClient
{
    private readonly RubricService.RubricServiceClient _client;
    private readonly ILogger<ExamCatalogGrpcClient> _logger;

    public ExamCatalogGrpcClient(RubricService.RubricServiceClient client, ILogger<ExamCatalogGrpcClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<SubjectGradingGridClientDto?> GetGradingGridAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var grid = await _client.GetGradingGridAsync(
                new SubjectRef { SubjectId = subjectId.ToString("D") }, cancellationToken: ct);

            var questions = grid.Questions
                .Select(q => new SubjectQuestionClientDto(
                    q.QuestionNumber,
                    q.HasGroupLabel ? q.GroupLabel : null,
                    q.HasLabel ? q.Label : null,
                    ParseDecimal(q.MaxScore),
                    q.OrderIndex))
                .ToList();

            return new SubjectGradingGridClientDto(
                Guid.Parse(grid.SubjectId),
                ParseDecimal(grid.MaxScore),
                grid.RubricVersion,
                questions,
                grid.HasStatus ? grid.Status : null);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "ExamCatalogService gRPC unreachable for subject {SubjectId} grading-grid", subjectId);
            return null;
        }
    }

    public async Task<SubjectExamInfoClientDto?> GetExamInfoAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var info = await _client.GetExamInfoAsync(
                new SubjectRef { SubjectId = subjectId.ToString("D") }, cancellationToken: ct);

            return new SubjectExamInfoClientDto(
                Guid.Parse(info.SubjectId),
                info.SubjectCode,
                Guid.Parse(info.ExamId),
                info.ExamName,
                info.HasExamEndDate ? ParseDate(info.ExamEndDate) : null,
                info.HasGradingDeadline ? ParseDate(info.GradingDeadline) : null);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "ExamCatalogService gRPC unreachable for subject {SubjectId} exam-info", subjectId);
            return null;
        }
    }

    public async Task<string?> GetRubricTextAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var text = await _client.GetRubricTextAsync(
                new SubjectRef { SubjectId = subjectId.ToString("D") }, cancellationToken: ct);
            return text.Text;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "ExamCatalogService gRPC unreachable for subject {SubjectId} rubric-text", subjectId);
            return null;
        }
    }

    public async Task<IReadOnlyList<RubricSourceFileClientDto>?> GetRubricSourceFilesAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var files = await _client.GetRubricSourceFilesAsync(
                new SubjectRef { SubjectId = subjectId.ToString("D") }, cancellationToken: ct);

            return files.Files
                .Select(f => new RubricSourceFileClientDto(f.Url, f.ContentType, f.Kind))
                .ToList();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "ExamCatalogService gRPC unreachable for subject {SubjectId} rubric-source-files", subjectId);
            return null;
        }
    }

    public async Task<CompiledRubricClientDto?> GetCompiledRubricAsync(
        Guid subjectId, int? rubricVersion, CancellationToken ct = default)
    {
        try
        {
            var request = new CompiledRubricRef { SubjectId = subjectId.ToString("D") };
            if (rubricVersion is { } v)
            {
                request.RubricVersion = v;
            }

            var contract = await _client.GetCompiledRubricAsync(request, cancellationToken: ct);

            var assets = contract.Assets
                .Select(a => new CompiledRubricAssetClientDto(
                    a.AssetId, a.Url, a.ContentType, a.HasQuestion ? a.Question : null))
                .ToList();

            return new CompiledRubricClientDto(
                contract.RubricVersion, contract.Status, contract.ContractJson, contract.CoverageOk, assets);
        }
        catch (RpcException ex)
        {
            // NotFound (no approved contract) is expected — fall back to the raw-file path.
            if (ex.StatusCode != StatusCode.NotFound)
            {
                _logger.LogError(ex, "ExamCatalogService gRPC error for subject {SubjectId} compiled-rubric", subjectId);
            }
            return null;
        }
    }

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}

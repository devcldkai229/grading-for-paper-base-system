using System.Text.Json;
using System.Text.Json.Nodes;
using Contracts.Messages;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Services;

/// <inheritdoc />
public sealed class GradingContractService : IGradingContractService
{
    private static readonly TimeSpan RubricPresignTtl = TimeSpan.FromMinutes(30);

    private readonly IExamCatalogRepository _repository;
    private readonly IS3Service _s3Service;
    private readonly IAiGradingClient _aiClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<GradingContractService> _logger;

    public GradingContractService(
        IExamCatalogRepository repository,
        IS3Service s3Service,
        IAiGradingClient aiClient,
        IPublishEndpoint publishEndpoint,
        ILogger<GradingContractService> logger)
    {
        _repository = repository;
        _s3Service = s3Service;
        _aiClient = aiClient;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> IngestAsync(Guid subjectId, CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            _logger.LogWarning("Ingest skipped: subject {SubjectId} not found", subjectId);
            return false;
        }

        var rubricInfo = await _repository.GetRubricInfoAsync(subjectId, ct);
        if (rubricInfo is not { } rubric || string.IsNullOrWhiteSpace(rubric.S3Key))
        {
            _logger.LogInformation("Ingest skipped: subject {SubjectId} has no rubric file", subjectId);
            return false;
        }

        var rubricUrl = await _s3Service.GeneratePresignedGetUrlAsync(
            rubric.S3Key, RubricPresignTtl, rubric.FileName, inline: true, ct);

        var request = new IngestRubricRequestDto(
            subjectId,
            rubric.RubricVersion.ToString(),
            subject.MaxScore,
            new[] { new IngestRubricFileDto(rubricUrl, rubric.ContentType, "rubric") },
            subject.Questions
                .Select(q => new IngestRubricScoreGridItemDto(
                    q.QuestionNumber, q.GroupLabel, q.Label, q.MaxScore, null))
                .ToList());

        var rawJson = await _aiClient.IngestRubricAsync(request, ct);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            _logger.LogWarning("Ingest failed: AI service returned no contract for {SubjectId}", subjectId);
            return false;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Ingest failed: AI returned invalid JSON for {SubjectId}", subjectId);
            return false;
        }

        var contractNode = root?["contract"] as JsonObject;
        if (contractNode is null)
        {
            _logger.LogError("Ingest failed: response missing 'contract' for {SubjectId}", subjectId);
            return false;
        }

        var modelUsed = root?["modelUsed"]?.GetValue<string>();
        var coverageOk = contractNode["coverageOk"]?.GetValue<bool>() ?? false;

        var assets = await PersistAssetsAsync(contractNode, subjectId, rubric.RubricVersion, ct);

        var contract = new GradingContract
        {
            SubjectId = subjectId,
            RubricVersion = rubric.RubricVersion,
            ContractJson = contractNode.ToJsonString(),
            CoverageOk = coverageOk,
            ModelUsed = modelUsed,
            Status = GradingContractStatus.Pending
        };

        await _repository.UpsertGradingContractAsync(contract, assets, ct);

        await _publishEndpoint.Publish(new RubricCompiledEvent(
            Guid.NewGuid(), subjectId, rubric.RubricVersion, coverageOk, null, DateTime.UtcNow), ct);

        _logger.LogInformation(
            "Compiled contract stored for subject {SubjectId} v{Version} ({AssetCount} assets, coverage_ok={Coverage})",
            subjectId, rubric.RubricVersion, assets.Count, coverageOk);
        return true;
    }

    /// <summary>Upload each base64 asset crop to S3, replace inline base64 in the contract JSON with
    /// the stored key, and return the RubricAsset rows to persist. URLs are presigned at read time.</summary>
    private async Task<List<RubricAsset>> PersistAssetsAsync(
        JsonObject contractNode, Guid subjectId, int rubricVersion, CancellationToken ct)
    {
        var assets = new List<RubricAsset>();
        if (contractNode["assets"] is not JsonArray assetArray)
        {
            return assets;
        }

        foreach (var node in assetArray)
        {
            if (node is not JsonObject assetObj)
            {
                continue;
            }

            var assetId = assetObj["assetId"]?.GetValue<string>();
            var base64 = assetObj["imageBase64"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(assetId) || string.IsNullOrWhiteSpace(base64))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                _logger.LogWarning("Skipping asset {AssetId}: invalid base64", assetId);
                continue;
            }

            var contentType = assetObj["mime"]?.GetValue<string>() ?? "image/png";
            var ext = contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
            var s3Key = $"rubric-assets/{subjectId}/v{rubricVersion}/{assetId}.{ext}";

            await _s3Service.UploadAsync(s3Key, new MemoryStream(bytes), contentType, ct);

            // Drop the heavy inline base64; keep a stable key so read-time can presign.
            assetObj.Remove("imageBase64");
            assetObj["s3Key"] = s3Key;

            assets.Add(new RubricAsset
            {
                SubjectId = subjectId,
                RubricVersion = rubricVersion,
                AssetId = assetId,
                Kind = assetObj["kind"]?.GetValue<string>() ?? "illustration",
                Question = assetObj["question"]?.GetValue<string>(),
                PageIndex = assetObj["pageIndex"]?.GetValue<int>() ?? 0,
                S3Key = s3Key,
                ContentType = contentType
            });
        }

        return assets;
    }
}

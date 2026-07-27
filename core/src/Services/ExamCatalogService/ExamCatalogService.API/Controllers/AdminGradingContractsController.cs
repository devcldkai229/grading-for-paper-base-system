using System.Text.Json;
using ExamCatalogService.API.Authorization;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCatalogService.API.Controllers;

/// <summary>
/// Admin review surface for compiled grading contracts (doc 4.5). A contract is the hard gate before
/// a subject/rubric version can be AI-graded with the new pipeline: admins review the AI-generated
/// check-items / partial-credit, optionally edit them, and approve or reject.
/// </summary>
[Route("api/admin/grading-contracts")]
[ApiController]
[Authorize]
[AdminOnly]
public class AdminGradingContractsController : ControllerBase
{
    private static readonly TimeSpan AssetTtl = TimeSpan.FromMinutes(30);

    private readonly IExamCatalogRepository _repository;
    private readonly IS3Service _s3Service;
    private readonly IGradingContractService _contractService;

    public AdminGradingContractsController(
        IExamCatalogRepository repository,
        IS3Service s3Service,
        IGradingContractService contractService)
    {
        _repository = repository;
        _s3Service = s3Service;
        _contractService = contractService;
    }

    /// <summary>List compiled contracts, risk-sorted (Pending + low coverage first).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct = default)
    {
        GradingContractStatus? filter = Enum.TryParse<GradingContractStatus>(status, true, out var s) ? s : null;
        var contracts = await _repository.ListGradingContractsAsync(filter, ct);

        var summaries = contracts
            .Select(ToSummary)
            // Risk-sorted: Pending first, then contracts flagged not-covered, then most warnings.
            .OrderByDescending(x => x.Status == nameof(GradingContractStatus.Pending))
            .ThenBy(x => x.CoverageOk)
            .ThenByDescending(x => x.WarningCount)
            .ToList();

        return Ok(new ApiResponse<IReadOnlyList<GradingContractSummaryDto>>
        {
            StatusCode = 200,
            Message = "Grading contracts",
            Data = summaries,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>Full contract JSON + presigned asset URLs for the review screen.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var contract = await _repository.GetGradingContractByIdAsync(id, ct);
        if (contract is null)
        {
            return NotFound(Fail(404, "Contract not found"));
        }

        var assets = await _repository.GetRubricAssetsAsync(contract.SubjectId, contract.RubricVersion, ct);
        var assetDtos = new List<RubricAssetDto>();
        foreach (var a in assets)
        {
            var url = await _s3Service.GeneratePresignedGetUrlAsync(a.S3Key, AssetTtl, inline: true, ct: ct);
            assetDtos.Add(new RubricAssetDto(a.AssetId, a.Kind, a.Question, a.PageIndex, url, a.ContentType));
        }

        var detail = new GradingContractDetailDto(
            contract.Id, contract.SubjectId, contract.RubricVersion, contract.Status.ToString(),
            contract.CoverageOk, contract.ModelUsed, contract.ContractJson, assetDtos,
            contract.CreatedAt, contract.ReviewedAt);

        return Ok(new ApiResponse<GradingContractDetailDto>
        {
            StatusCode = 200,
            Message = "Grading contract",
            Data = detail,
            ResponsedAt = DateTime.UtcNow
        });
    }

    public record ReviewContractRequest(string? ContractJson);

    /// <summary>Approve (optionally with edited check-items/partial-credit). Approval is the grading gate.</summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewContractRequest? body, CancellationToken ct = default)
    {
        var reviewer = GetUserId();
        var ok = await _repository.UpdateGradingContractReviewAsync(
            id, GradingContractStatus.Approved, reviewer, body?.ContractJson, ct);
        return ok
            ? Ok(Success("Contract approved"))
            : NotFound(Fail(404, "Contract not found"));
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewContractRequest? body, CancellationToken ct = default)
    {
        var reviewer = GetUserId();
        var ok = await _repository.UpdateGradingContractReviewAsync(
            id, GradingContractStatus.Rejected, reviewer, body?.ContractJson, ct);
        return ok
            ? Ok(Success("Contract rejected"))
            : NotFound(Fail(404, "Contract not found"));
    }

    /// <summary>Force a fresh compile for a subject's current rubric version.</summary>
    [HttpPost("/api/admin/subjects/{subjectId:guid}/grading-contracts/recompile")]
    public async Task<IActionResult> Recompile(Guid subjectId, CancellationToken ct = default)
    {
        // Ingest can take several minutes (Gotenberg + LLM). Do not bind to the request abort
        // token — browsers/gateways cancel long POSTs and leave no persisted contract.
        var ok = await _contractService.IngestAsync(subjectId, CancellationToken.None);
        return ok
            ? Ok(Success("Recompiled"))
            : StatusCode(502, Fail(502, "Recompile failed (no rubric or AI unreachable)"));
    }

    /// <summary>Latest compiled contract for a subject (any non-rejected), or 404.</summary>
    [HttpGet("/api/admin/subjects/{subjectId:guid}/grading-contracts/latest")]
    public async Task<IActionResult> GetLatestForSubject(Guid subjectId, CancellationToken ct = default)
    {
        var contract = await _repository.GetApprovedGradingContractAsync(subjectId, null, ct);
        if (contract is null)
        {
            return NotFound(Fail(404, "No grading contract for this subject yet"));
        }

        return Ok(new ApiResponse<GradingContractSummaryDto>
        {
            StatusCode = 200,
            Message = "Latest grading contract",
            Data = ToSummary(contract),
            ResponsedAt = DateTime.UtcNow
        });
    }

    private static GradingContractSummaryDto ToSummary(GradingContract c)
    {
        var (criteria, assets, warnings) = CountFromJson(c.ContractJson);
        return new GradingContractSummaryDto(
            c.Id, c.SubjectId, c.RubricVersion, c.Status.ToString(), c.CoverageOk, c.ModelUsed,
            criteria, assets, warnings, c.CreatedAt, c.ReviewedAt);
    }

    private static (int Criteria, int Assets, int Warnings) CountFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int Count(string prop) =>
                root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Array
                    ? el.GetArrayLength() : 0;
            return (Count("criteria"), Count("assets"), Count("warnings"));
        }
        catch (JsonException)
        {
            return (0, 0, 0);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("UserId")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static ApiResponse<object> Fail(int code, string message) =>
        new() { StatusCode = code, Message = message, Data = null!, ResponsedAt = DateTime.UtcNow };

    private static ApiResponse<object> Success(string message) =>
        new() { StatusCode = 200, Message = message, Data = null!, ResponsedAt = DateTime.UtcNow };
}

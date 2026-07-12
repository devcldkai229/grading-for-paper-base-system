using GradingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GradingService.API.Controllers;

/// <summary>
/// Internal (service-to-service) endpoints about marker assignments.
/// Consumed by SubmissionService to enforce lecturer authorization (alias ranges).
/// NOTE: intended for internal use only — restrict at network/gateway level (do not expose publicly).
/// </summary>
[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly GradingDbContext _db;

    public AssignmentsController(GradingDbContext db) => _db = db;

    public record AliasRangeItem(int AliasStart, int AliasEnd);

    /// <summary>
    /// Returns ALL alias ranges assigned to a lecturer for a subject.
    /// A lecturer may have multiple ranges (multiple marker_assignments rows).
    /// </summary>
    [HttpGet("subjects/{subjectId:guid}/lecturers/{lecturerId:guid}/alias-ranges")]
    public async Task<IActionResult> GetAliasRanges(Guid subjectId, Guid lecturerId, CancellationToken ct = default)
    {
        var ranges = await _db.MarkerAssignments
            .AsNoTracking()
            .Where(m => m.SubjectId == subjectId && m.TeacherId == lecturerId)
            .OrderBy(m => m.AliasStart)
            .Select(m => new AliasRangeItem(m.AliasStart, m.AliasEnd))
            .ToListAsync(ct);

        // Always 200 with (possibly empty) list. Empty => lecturer has no assignment => no access.
        return Ok(new { data = ranges });
    }

    /// <summary>
    /// Returns the distinct subject ids a lecturer has any marker assignment for.
    /// Consumed by ExamCatalogService to scope subject search results to a Lecturer's own subjects.
    /// </summary>
    [HttpGet("lecturers/{lecturerId:guid}/subjects")]
    public async Task<IActionResult> GetAssignedSubjectIds(Guid lecturerId, CancellationToken ct = default)
    {
        var subjectIds = await _db.MarkerAssignments
            .AsNoTracking()
            .Where(m => m.TeacherId == lecturerId)
            .Select(m => m.SubjectId)
            .Distinct()
            .ToListAsync(ct);

        // Always 200 with (possibly empty) list. Empty => lecturer has no assignment => no access.
        return Ok(new { data = subjectIds });
    }
}

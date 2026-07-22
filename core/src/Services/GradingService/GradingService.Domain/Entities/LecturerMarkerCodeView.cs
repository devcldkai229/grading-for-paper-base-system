namespace GradingService.Domain.Entities;

/// <summary>
/// Read-only local projection of lecturer profile data owned by IamService, kept in sync via
/// LecturerProfileChanged events. Lets grade export resolve a teacher's marker code WITHOUT a
/// synchronous bulk pull of the whole user list (N5 — no availability coupling). <see cref="UserId"/>
/// mirrors the IAM user id so upserts are exact and naturally idempotent (N8).
/// </summary>
public class LecturerMarkerCodeView
{
    public Guid UserId { get; set; }
    public string? MarkerCode { get; set; }
    public string? FullName { get; set; }
    public DateTime UpdatedAt { get; set; }
}

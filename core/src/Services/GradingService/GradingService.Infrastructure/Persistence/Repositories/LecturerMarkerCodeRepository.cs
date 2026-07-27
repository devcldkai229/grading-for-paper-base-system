using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class LecturerMarkerCodeRepository : ILecturerMarkerCodeRepository
{
    private readonly GradingDbContext _db;

    public LecturerMarkerCodeRepository(GradingDbContext db) => _db = db;

    public async Task<Dictionary<Guid, string>> GetAllMarkerCodesAsync(CancellationToken ct = default)
    {
        return await _db.LecturerMarkerCodeViews
            .AsNoTracking()
            .Where(v => v.MarkerCode != null && v.MarkerCode != "")
            .ToDictionaryAsync(v => v.UserId, v => v.MarkerCode!, ct);
    }

    public async Task UpsertAsync(
        Guid userId, string? markerCode, string? fullName, DateTime updatedAt, CancellationToken ct = default)
    {
        var existing = await _db.LecturerMarkerCodeViews
            .FirstOrDefaultAsync(v => v.UserId == userId, ct);

        if (existing is null)
        {
            _db.LecturerMarkerCodeViews.Add(new LecturerMarkerCodeView
            {
                UserId = userId,
                MarkerCode = markerCode,
                FullName = fullName,
                UpdatedAt = updatedAt
            });
        }
        else
        {
            existing.MarkerCode = markerCode;
            existing.FullName = fullName;
            existing.UpdatedAt = updatedAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}

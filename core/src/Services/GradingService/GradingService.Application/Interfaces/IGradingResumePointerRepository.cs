using GradingService.Domain.Entities;

namespace GradingService.Application.Interfaces;

public interface IGradingResumePointerRepository
{
    Task<GradingResumePointer?> GetAsync(
        Guid teacherId, Guid batchId, bool asNoTracking, CancellationToken ct = default);

    /// <summary>Stages a new resume pointer for insertion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Add(GradingResumePointer pointer);
}

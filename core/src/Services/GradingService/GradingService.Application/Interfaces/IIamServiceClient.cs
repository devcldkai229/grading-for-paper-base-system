using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GradingService.Application.Interfaces;

public interface IIamServiceClient
{
    Task<Dictionary<Guid, string>> GetAllLecturerMarkerCodesAsync(CancellationToken ct = default);
}

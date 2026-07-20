using IamService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IamService.API.Controller;

/// <summary>
/// Internal (service-to-service) endpoint for querying users.
/// Protected by InternalApiKeyMiddleware (path prefix /api/internal) — no JWT/[Authorize] here.
/// </summary>
[Route("api/internal/users")]
[ApiController]
public class InternalUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public InternalUsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 1000,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 5000) pageSize = 1000;

        var result = await _userService.GetUsersAsync(page, pageSize, search);

        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }
}

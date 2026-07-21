using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IUserRepository _repo;
    public DashboardController(IUserRepository repo) => _repo = repo;

    // sp_GetDashboard -> vw_UserDashboard
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var dashboard = await _repo.GetDashboardAsync(userId);
        return dashboard is null ? NotFound() : Ok(dashboard);
    }
}

[ApiController]
[Route("api/databases")]
[Authorize]
public class DatabasesController : ControllerBase
{
    private readonly IUserRepository _repo;
    public DatabasesController(IUserRepository repo) => _repo = repo;

    // sp_GetCredentials -> registra CREDENTIALS_VIEWED en AuditLog con la IP
    [HttpGet("credentials")]
    public async Task<ActionResult<ConnectionInfoDto>> GetCredentials()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var info = await _repo.GetCredentialsAsync(userId, ip);
        return info is null ? NotFound() : Ok(info);
    }
}

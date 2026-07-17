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

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var dashboard = await _repo.GetDashboardAsync(userId);
        return Ok(dashboard);
    }
}

[ApiController]
[Route("api/databases")]
[Authorize]
public class DatabasesController : ControllerBase
{
    private readonly IUserRepository _repo;
    public DatabasesController(IUserRepository repo) => _repo = repo;

    [HttpGet("mine")]
    public async Task<ActionResult<ConnectionInfoDto>> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var info = await _repo.GetUserDatabaseAsync(userId);
        return info is null ? NotFound() : Ok(info);
    }
}
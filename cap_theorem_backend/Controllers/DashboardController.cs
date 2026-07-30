using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.Services;

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
    private readonly IMySqlProvisioningService _provisioning;
    public DatabasesController(IUserRepository repo, IMySqlProvisioningService provisioning)
    {
        _repo = repo;
        _provisioning = provisioning;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<ConnectionInfoDto>> GetMine()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var info = await _repo.GetUserDatabaseAsync(userId);
        if (info is null) return NotFound();

        var decrypted = info with { Password = _provisioning.Decrypt(info.Password) };
        return Ok(decrypted);
    }
}
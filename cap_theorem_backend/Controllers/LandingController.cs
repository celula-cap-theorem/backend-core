using Microsoft.AspNetCore.Mvc;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Controllers;

[ApiController]
[Route("api/landing")]
public class LandingController : ControllerBase
{
    private readonly IUserRepository _repo;
    public LandingController(IUserRepository repo) => _repo = repo;

    // sp_GetLandingMetrics -> vw_LandingMetrics
    [HttpGet("metrics")]
    public async Task<ActionResult<LandingMetricsDto>> Metrics() => Ok(await _repo.GetLandingMetricsAsync());
}

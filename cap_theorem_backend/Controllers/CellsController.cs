using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

/// <summary>
/// Control-plane endpoints: creating cells, provisioning/pausing/deleting
/// tenants, and listing global quotas. Restricted to SuperAdmin via
/// [Authorize(Roles = "SuperAdmin")] — a normal user's JWT never carries
/// that role claim, so it bounces off with 403 before touching any logic.
/// </summary>
[ApiController]
[Route("api/admin/cells")]
[Authorize(Roles = "SuperAdmin")]
public class CellsController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;

    public CellsController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    [HttpPost]
    public async Task<ActionResult<CellDto>> CreateCell([FromBody] CreateCellRequest request)
    {
        var cell = await _catalogRepository.CreateCellAsync(request);
        return CreatedAtAction(nameof(CreateCell), new { id = cell.Id }, cell);
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        // The stored procedure triggers (or enqueues via Hangfire) the
        // physical database creation, MAXSIZE and the dedicated login.
        // See section 7 of the architecture proposal.
        var tenant = await _catalogRepository.CreateTenantAsync(request);
        return CreatedAtAction(nameof(CreateTenant), new { id = tenant.Id }, tenant);
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<IEnumerable<TenantDto>>> ListTenants()
    {
        var tenants = await _catalogRepository.ListTenantsAsync();
        return Ok(tenants);
    }

    [HttpPost("tenants/{tenantId:int}/pause")]
    public async Task<ActionResult> PauseTenant(int tenantId)
    {
        var ok = await _catalogRepository.PauseTenantAsync(tenantId);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("tenants/{tenantId:int}")]
    public async Task<ActionResult> DeleteTenant(int tenantId)
    {
        var ok = await _catalogRepository.DeleteTenantAsync(tenantId);
        return ok ? NoContent() : NotFound();
    }
}

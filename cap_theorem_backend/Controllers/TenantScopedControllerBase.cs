using cap_theorem_backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

/// <summary>
/// Base for any controller exposing tenant resources (e.g. BookingsController,
/// and any future business resource). Centralizes the validation from
/// section 6.1 of the proposal: the JWT's tenant_id must match the tenant
/// resolved from the path; if it doesn't match, 403 without touching the
/// database.
/// </summary>
[Authorize(Roles = "User")]
public abstract class TenantScopedControllerBase : ControllerBase
{
    protected readonly ITenantContext TenantContext;

    protected TenantScopedControllerBase(ITenantContext tenantContext)
    {
        TenantContext = tenantContext;
    }

    /// <summary>
    /// Call at the start of every action. Returns a 403 if the token's
    /// tenant_id doesn't match the tenant resolved from the current URL.
    /// </summary>
    protected ActionResult? ValidateTenantFromToken()
    {
        var tenantIdClaim = User.FindFirst("tenant_id")?.Value;

        if (tenantIdClaim is null || !int.TryParse(tenantIdClaim, out var tokenTenantId))
        {
            return Forbid();
        }

        if (tokenTenantId != TenantContext.TenantId)
        {
            return Forbid();
        }

        return null; // valid, continue with the action
    }
}

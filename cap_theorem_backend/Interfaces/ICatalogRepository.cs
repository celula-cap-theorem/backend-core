using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.DTOs.Catalog;

namespace cap_theorem_backend.Interfaces;

/// <summary>
/// Always operates against the catalog database (control plane).
/// Resolves routes (cell/tenant -> connection string), quotas,
/// tenant provisioning/teardown, and authentication credentials.
/// The concrete implementation only invokes catalog stored procedures/views,
/// never builds dynamic SQL.
/// </summary>
public interface ICatalogRepository
{
    // --- Route resolution (used by the tenant middleware) ---
    Task<TenantConnectionInfo?> ResolveTenantConnectionAsync(string cellSlug, string tenantSlug);

    // --- Provisioning (superadmin only) ---
    Task<CellDto> CreateCellAsync(CreateCellRequest request);
    Task<TenantDto> CreateTenantAsync(CreateTenantRequest request);
    Task<bool> PauseTenantAsync(int tenantId);
    Task<bool> DeleteTenantAsync(int tenantId);
    Task<IEnumerable<TenantDto>> ListTenantsAsync();

    // --- Authentication (normal users and superadmin) ---
    Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email);
    Task<UserCredentialsDto?> GetSuperAdminCredentialsByEmailAsync(string email);
    Task<int> RegisterUserAsync(string email, string passwordHash, int tenantId);
}

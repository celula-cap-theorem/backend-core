using System.Data;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.DTOs.Catalog;
using Dapper;
using Microsoft.Data.SqlClient;

namespace cap_theorem_backend.Repositories;

/// <summary>
/// GOLDEN RULE: this class contains no business logic. It only invokes
/// stored procedures/views on the CATALOG database (control plane) with
/// typed parameters via Dapper. Quotas, TTL and provisioning validation
/// live in the stored procedures, not in C#.
///
/// TODO: replace the stored procedure names (sp_...) with the real ones
/// once they exist in the catalog database.
/// </summary>
public class CatalogRepository : ICatalogRepository
{
    private readonly string _catalogConnectionString;

    public CatalogRepository(IConfiguration configuration)
    {
        _catalogConnectionString = configuration.GetConnectionString("Catalog")
            ?? throw new InvalidOperationException(
                "Connection string 'Catalog' is not configured in appsettings.json.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_catalogConnectionString);

    public async Task<TenantConnectionInfo?> ResolveTenantConnectionAsync(string cellSlug, string tenantSlug)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantConnectionInfo>(
            "sp_ResolveTenantConnection",
            new { CellSlug = cellSlug, TenantSlug = tenantSlug },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<CellDto> CreateCellAsync(CreateCellRequest request)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleAsync<CellDto>(
            "sp_CreateCell",
            new { request.Name },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantRequest request)
    {
        // Note: the stored procedure triggers (or enqueues via Hangfire) the
        // physical database creation, the ALTER DATABASE ... MAXSIZE, and
        // the dedicated login. This repository only invokes the SP and
        // returns the catalog record.
        using var connection = CreateConnection();
        return await connection.QuerySingleAsync<TenantDto>(
            "sp_CreateTenant",
            new { request.CellId, request.Slug },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> PauseTenantAsync(int tenantId)
    {
        using var connection = CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(
            "sp_PauseTenant",
            new { TenantId = tenantId },
            commandType: CommandType.StoredProcedure);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteTenantAsync(int tenantId)
    {
        using var connection = CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(
            "sp_DeleteTenant",
            new { TenantId = tenantId },
            commandType: CommandType.StoredProcedure);
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<TenantDto>> ListTenantsAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<TenantDto>(
            "sp_ListTenants",
            commandType: CommandType.StoredProcedure);
    }

    public async Task<UserCredentialsDto?> GetCredentialsByEmailAsync(string email)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserCredentialsDto>(
            "sp_GetUserCredentials",
            new { Email = email },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<UserCredentialsDto?> GetSuperAdminCredentialsByEmailAsync(string email)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserCredentialsDto>(
            "sp_GetSuperAdminCredentials",
            new { Email = email },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> RegisterUserAsync(string email, string passwordHash, int tenantId)
    {
        using var connection = CreateConnection();
        return await connection.QuerySingleAsync<int>(
            "sp_RegisterUser",
            new { Email = email, PasswordHash = passwordHash, TenantId = tenantId },
            commandType: CommandType.StoredProcedure);
    }
}

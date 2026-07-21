using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Repositories;

/// <summary>
/// REGLA DE ORO: esta clase no contiene lógica de negocio. Solo invoca los
/// Stored Procedures de ProvisioningControl con parámetros tipados (Dapper).
/// Todo el cálculo, cifrado y validación vive en SQL Server.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("ProvisioningControl")
            ?? throw new InvalidOperationException("Connection string 'ProvisioningControl' is not configured.");

    private IDbConnection Conn() => new SqlConnection(_connectionString);

    public async Task<UserDto> RegisterOrUpdateUserAsync(
        string provider, string providerUserId, string fullName, string email, string? avatar, string? ipAddress)
    {
        using var c = Conn();
        return await c.QuerySingleAsync<UserDto>(
            "sp_RegisterOrUpdateUser",
            new
            {
                Provider = provider,
                ProviderUserId = providerUserId,
                FullName = fullName,
                Email = email,
                Avatar = avatar,
                IpAddress = ipAddress
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ProvisionResultDto> ProvisionDatabaseAsync(int userId, string host, string plainPassword, string? ipAddress)
    {
        using var c = Conn();
        return await c.QuerySingleAsync<ProvisionResultDto>(
            "sp_ProvisionDatabase",
            new { UserId = userId, Host = host, PlainPassword = plainPassword, IpAddress = ipAddress },
            commandType: CommandType.StoredProcedure);
    }

    public async Task ConfirmProvisioningAsync(int databaseId, bool success, string? errorDetail)
    {
        using var c = Conn();
        await c.ExecuteAsync(
            "sp_ConfirmProvisioning",
            new { DatabaseId = databaseId, Success = success, ErrorDetail = errorDetail },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<DashboardDto?> GetDashboardAsync(int userId)
    {
        using var c = Conn();
        return await c.QuerySingleOrDefaultAsync<DashboardDto>(
            "sp_GetDashboard", new { UserId = userId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<ConnectionInfoDto?> GetCredentialsAsync(int userId, string? ipAddress)
    {
        using var c = Conn();
        return await c.QuerySingleOrDefaultAsync<ConnectionInfoDto>(
            "sp_GetCredentials", new { UserId = userId, IpAddress = ipAddress }, commandType: CommandType.StoredProcedure);
    }

    public async Task<LandingMetricsDto> GetLandingMetricsAsync()
    {
        using var c = Conn();
        return await c.QuerySingleAsync<LandingMetricsDto>(
            "sp_GetLandingMetrics", commandType: CommandType.StoredProcedure);
    }
}

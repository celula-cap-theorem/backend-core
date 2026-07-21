using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Interfaces;

/// <summary>
/// Contrato de persistencia. La implementación concreta solo invoca los
/// Stored Procedures / Views de la base ProvisioningControl (SQL Server) —
/// nunca contiene reglas de negocio (DIP).
/// </summary>
public interface IUserRepository
{
    /// sp_RegisterOrUpdateUser
    Task<UserDto> RegisterOrUpdateUserAsync(
        string provider, string providerUserId, string fullName, string email, string? avatar, string? ipAddress);

    /// sp_ProvisionDatabase
    Task<ProvisionResultDto> ProvisionDatabaseAsync(int userId, string host, string plainPassword, string? ipAddress);

    /// sp_ConfirmProvisioning
    Task ConfirmProvisioningAsync(int databaseId, bool success, string? errorDetail);

    /// sp_GetDashboard
    Task<DashboardDto?> GetDashboardAsync(int userId);

    /// sp_GetCredentials
    Task<ConnectionInfoDto?> GetCredentialsAsync(int userId, string? ipAddress);

    /// sp_GetLandingMetrics
    Task<LandingMetricsDto> GetLandingMetricsAsync();
}

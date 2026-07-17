using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Interfaces;

public interface IUserRepository
{
    Task<(UserDto User, bool IsNew)> UpsertOAuthUserAsync(string provider, string providerUserId, string email, string name, string? avatarUrl);
    Task<DatabaseSlotDto> ReserveDatabaseSlotAsync(int userId);
    Task ConfirmProvisioningAsync(int userId, string dbName, string dbUser, string encryptedPassword, string host, int port, string engine);
    Task<ConnectionInfoDto?> GetUserDatabaseAsync(int userId);
    Task<DashboardDto> GetDashboardAsync(int userId);
    Task<LandingMetricsDto> GetLandingMetricsAsync();
}
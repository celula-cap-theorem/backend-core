using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;

namespace cap_theorem_backend.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _catalogConnectionString;

    public UserRepository(IConfiguration configuration) =>
        _catalogConnectionString = configuration.GetConnectionString("Catalog")!;

    private IDbConnection Conn() => new SqlConnection(_catalogConnectionString);

    public async Task<(UserDto, bool)> UpsertOAuthUserAsync(string provider, string providerUserId, string email, string name, string? avatarUrl)
    {
        using var c = Conn();
        var result = await c.QuerySingleAsync<UserDto>(
            "sp_UpsertOAuthUser",
            new { Provider = provider, ProviderUserId = providerUserId, Email = email, Name = name, AvatarUrl = avatarUrl },
            commandType: CommandType.StoredProcedure);
        return (result, result.IsNewUser);
    }

    public async Task<DatabaseSlotDto> ReserveDatabaseSlotAsync(int userId)
    {
        using var c = Conn();
        return await c.QuerySingleAsync<DatabaseSlotDto>(
            "sp_ReserveDatabaseSlot", new { UserId = userId }, commandType: CommandType.StoredProcedure);
    }

    public async Task ConfirmProvisioningAsync(int userId, string dbName, string dbUser, string encryptedPassword, string host, int port, string engine)
    {
        using var c = Conn();
        await c.ExecuteAsync("sp_ConfirmProvisioning",
            new { UserId = userId, DbName = dbName, DbUser = dbUser, PasswordEncrypted = encryptedPassword, Host = host, Port = port, Engine = engine },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ConnectionInfoDto?> GetUserDatabaseAsync(int userId)
    {
        using var c = Conn();
        return await c.QuerySingleOrDefaultAsync<ConnectionInfoDto>(
            "sp_GetUserDatabase", new { UserId = userId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<DashboardDto> GetDashboardAsync(int userId)
    {
        using var c = Conn();
        return await c.QuerySingleAsync<DashboardDto>(
            "SELECT * FROM vw_UserDashboard WHERE UserId = @UserId", new { UserId = userId });
    }

    public async Task<LandingMetricsDto> GetLandingMetricsAsync()
    {
        using var c = Conn();
        return await c.QuerySingleAsync<LandingMetricsDto>("SELECT * FROM vw_LandingMetrics");
    }
}
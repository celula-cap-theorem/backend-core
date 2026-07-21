namespace cap_theorem_backend.DTOs.Auth;

// Resultado de sp_RegisterOrUpdateUser
public record UserDto(int UserId, bool IsExistingUser);

// Resultado de sp_ProvisionDatabase
public record ProvisionResultDto(int DatabaseId, string DatabaseName, string MysqlUsername, string Host, int Port);

// Resultado de sp_GetDashboard (proyección de vw_UserDashboard)
public record DashboardDto(
    string Host,
    int Port,
    string DatabaseName,
    string MysqlUsername,
    string Engine,
    DateTime CreatedAt,
    string Status,
    decimal UsedSpaceMB,
    decimal MaxSpaceMB,
    DateTime? LastActivityAt);

// Resultado de sp_GetCredentials (vw_ConnectionCredentials desencriptada)
public record ConnectionInfoDto(
    string Host,
    int Port,
    string DatabaseName,
    string MysqlUsername,
    string Password,
    string Engine,
    DateTime CreatedAt,
    string Status);

// Resultado de sp_GetLandingMetrics (vw_LandingMetrics)
public record LandingMetricsDto(
    int TotalUsers,
    int TotalDatabases,
    int ActiveDatabases,
    long TotalLogins,
    int ActiveUsers,
    decimal Availability);

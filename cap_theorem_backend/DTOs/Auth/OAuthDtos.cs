namespace cap_theorem_backend.DTOs.Auth;

public record UserDto(int UserId, string Email, string Name, string? AvatarUrl, string Provider, bool IsNewUser);
public record DatabaseSlotDto(string DbName, string DbUser, string Host, int Port);
public record ConnectionInfoDto(string Host, int Port, string DbName, string DbUser, string Engine, DateTime CreatedAt, string Status);
public record DashboardDto(string Status, long UsedBytes, long MaxBytes, DateTime CreatedAt, DateTime LastActivity);
public record LandingMetricsDto(int TotalUsers, int TotalDatabases, int ActiveDatabases, long TotalLogins, int ActiveUsers, string Availability);
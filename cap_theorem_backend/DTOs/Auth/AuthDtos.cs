namespace cap_theorem_backend.DTOs.Auth;

public record RegisterUserRequest(string Email, string Password, int TenantId);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAt);

/// <summary>
/// Represents the record returned by the catalog when validating credentials.
/// Contains the hash, never the plaintext password. The hash comparison
/// happens in the backend (BCrypt), not in the stored procedure: it's a
/// cross-cutting security concern, not a business rule of the challenge.
/// </summary>
public record UserCredentialsDto(
    int UserId,
    string Email,
    string PasswordHash,
    int? TenantId,
    string Role // "User" or "SuperAdmin"
);

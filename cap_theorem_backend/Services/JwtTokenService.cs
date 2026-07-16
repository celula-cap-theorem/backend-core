using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace cap_theorem_backend.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateUserToken(int userId, string email, int tenantId);
    (string Token, DateTime ExpiresAt) GenerateSuperAdminToken(int userId, string email);
}

/// <summary>
/// The only place in the backend that signs JWTs. The claim structure is
/// what lets the authorization middleware (declarative, [Authorize(Roles=...)])
/// separate normal users from superadmin without any extra business logic:
/// a normal user ALWAYS carries tenant_id, a superadmin NEVER does.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateUserToken(int userId, string email, int tenantId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, "User"),
            new("tenant_id", tenantId.ToString())
        };

        return GenerateToken(claims);
    }

    public (string Token, DateTime ExpiresAt) GenerateSuperAdminToken(int userId, string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, "SuperAdmin")
            // No tenant_id on purpose: its scope is the entire catalog.
        };

        return GenerateToken(claims);
    }

    private (string Token, DateTime ExpiresAt) GenerateToken(List<Claim> claims)
    {
        // TODO: move Jwt:Key to a real secret (User Secrets / environment
        // variable) before deploying. Never commit the real key in
        // appsettings.json.
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

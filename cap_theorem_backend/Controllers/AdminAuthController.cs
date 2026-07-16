using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

/// <summary>
/// Authentication flow EXCLUSIVE to superadmin. On purpose, there is NO
/// registration endpoint here: superadmin accounts are created via
/// seed/migration or manual invitation, never through a public form.
/// This reduces the attack surface for privilege escalation.
/// </summary>
[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public AdminAuthController(ICatalogRepository catalogRepository, IJwtTokenService jwtTokenService)
    {
        _catalogRepository = catalogRepository;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var admin = await _catalogRepository.GetSuperAdminCredentialsByEmailAsync(request.Email);
        if (admin is null || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        // TODO: this is where the 2FA check recommended by the architecture
        // proposal would go, before issuing the token.

        var (token, expiresAt) = _jwtTokenService.GenerateSuperAdminToken(admin.UserId, admin.Email);
        return Ok(new AuthResponse(token, expiresAt));
    }
}

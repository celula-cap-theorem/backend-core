using BCrypt.Net;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

/// <summary>
/// PUBLIC authentication flow for normal users (a cell's students).
/// Unrelated to /api/admin/auth: these are separate flows on purpose, to
/// avoid any surface for escalating into superadmin.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(ICatalogRepository catalogRepository, IJwtTokenService jwtTokenService)
    {
        _catalogRepository = catalogRepository;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterUserRequest request)
    {
        var existing = await _catalogRepository.GetCredentialsByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { error = "A user with that email already exists." });
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userId = await _catalogRepository.RegisterUserAsync(request.Email, passwordHash, request.TenantId);

        return CreatedAtAction(nameof(Register), new { Id = userId });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _catalogRepository.GetCredentialsByEmailAsync(request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        if (user.TenantId is null)
        {
            // Should never happen: a normal user always belongs to a tenant.
            return Unauthorized(new { error = "Account has no associated tenant." });
        }

        var (token, expiresAt) = _jwtTokenService.GenerateUserToken(user.UserId, user.Email, user.TenantId.Value);
        return Ok(new AuthResponse(token, expiresAt));
    }
}

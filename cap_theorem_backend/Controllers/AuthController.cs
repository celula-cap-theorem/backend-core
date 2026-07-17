using System.Security.Claims;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Auth;
using cap_theorem_backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IMySqlProvisioningService _provisioningService;
    private readonly IConfiguration _config;

    public AuthController(
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IMySqlProvisioningService provisioningService,
        IConfiguration config)
    {
        _userRepository = userRepository;
        _jwtTokenService = jwtTokenService;
        _provisioningService = provisioningService;
        _config = config;
    }

    [HttpGet("google")]
    public IActionResult GoogleLogin() =>
        Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/callback/google" },
            GoogleDefaults.AuthenticationScheme);

    [HttpGet("github")]
    public IActionResult GitHubLogin() =>
        Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/callback/github" },
            GitHubAuthenticationDefaults.AuthenticationScheme);

    [HttpGet("callback/google")]
    public Task<IActionResult> GoogleCallback() => HandleExternalCallback("Google");

    [HttpGet("callback/github")]
    public Task<IActionResult> GitHubCallback() => HandleExternalCallback("GitHub");

    private async Task<IActionResult> HandleExternalCallback(string provider)
    {
        var result = await HttpContext.AuthenticateAsync("External");
        if (!result.Succeeded || result.Principal is null)
            return Unauthorized(new { error = "OAuth handshake failed." });

        var claims = result.Principal;
        var providerUserId = claims.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = claims.FindFirstValue(ClaimTypes.Email)!;
        var name = claims.FindFirstValue(ClaimTypes.Name) ?? email;
        var avatar = claims.Claims.FirstOrDefault(c =>
            c.Type is "urn:google:picture" or "urn:github:avatar")?.Value;

        var (user, isNew) = await _userRepository.UpsertOAuthUserAsync(provider, providerUserId, email, name, avatar);

        if (isNew)
        {
            var slot = await _userRepository.ReserveDatabaseSlotAsync(user.UserId);
            var (dbUser, password) = _provisioningService.GenerateCredentials(slot.DbUser);
            await _provisioningService.CreateDatabaseAsync(slot.DbName, dbUser, password);
            await _userRepository.ConfirmProvisioningAsync(
                user.UserId, slot.DbName, dbUser,
                _provisioningService.Encrypt(password), slot.Host, slot.Port, "MySQL");
        }

        var (token, expiresAt) = _jwtTokenService.GenerateUserToken(user.UserId, user.Email, tenantId: user.UserId);
        await HttpContext.SignOutAsync("External");

        var redirect = $"{_config["Frontend:PostLoginRedirect"]}?token={token}&expires={expiresAt:o}";
        return Redirect(redirect);
    }
}
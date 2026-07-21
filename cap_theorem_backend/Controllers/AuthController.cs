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
    public Task<IActionResult> GoogleCallback() => HandleExternalCallback("google");

    [HttpGet("callback/github")]
    public Task<IActionResult> GitHubCallback() => HandleExternalCallback("github");

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
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // sp_RegisterOrUpdateUser decide internamente si es alta o solo
        // actualización de LastLoginAt; el backend no contiene esa regla.
        var user = await _userRepository.RegisterOrUpdateUserAsync(provider, providerUserId, name, email, avatar, ip);

        if (!user.IsExistingUser)
        {
            var mysqlHost = _config["MySqlAdmin:Host"]!;
            var plainPassword = _provisioningService.GenerateSecurePassword();

            // sp_ProvisionDatabase genera DbName/DbUser (fn_GenerateDbName),
            // cifra y guarda la contraseña, y registra el Pending.
            var slot = await _userRepository.ProvisionDatabaseAsync(user.UserId, mysqlHost, plainPassword, ip);

            try
            {
                await _provisioningService.CreateDatabaseAsync(slot.DatabaseName, slot.MysqlUsername, plainPassword);
                await _userRepository.ConfirmProvisioningAsync(slot.DatabaseId, success: true, errorDetail: null);
            }
            catch (Exception ex)
            {
                await _userRepository.ConfirmProvisioningAsync(slot.DatabaseId, success: false, errorDetail: ex.Message);
            }
        }

        var (token, expiresAt) = _jwtTokenService.GenerateUserToken(user.UserId, email);
        await HttpContext.SignOutAsync("External");

        var redirect = $"{_config["Frontend:PostLoginRedirect"]}?token={token}&expires={expiresAt:o}";
        return Redirect(redirect);
    }
}

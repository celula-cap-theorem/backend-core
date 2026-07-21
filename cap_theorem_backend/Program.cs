using System.Text;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.Repositories;
using cap_theorem_backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using AspNet.Security.OAuth.GitHub;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

// --- JWT authentication ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? string.Empty))
        };
    })
    .AddCookie("External")
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = "External";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/api/auth/callback/google";
    })
    .AddGitHub(GitHubAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = "External";
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]!;
        options.CallbackPath = "/api/auth/callback/github";
        options.Scope.Add("user:email");
    });

builder.Services.AddAuthorization();

// --- Rate limiting (per IP) ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("PerIpPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// --- Dependency injection ---
// Único repositorio: opera contra ProvisioningControl (SQL Server) invocando
// exclusivamente Stored Procedures. Nunca contiene reglas de negocio (DIP).
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMySqlProvisioningService, MySqlProvisioningService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("PerIpPolicy");

app.Run();

using System.Text;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.Infrastructure;
using cap_theorem_backend.Middleware;
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

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMySqlProvisioningService, MySqlProvisioningService>();
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
// Control plane: always against the catalog.
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();

// Data plane: scoped context + factory that resolves the current tenant's connection.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITenantConnectionFactory, TenantConnectionFactory>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Resolves cell/tenant BEFORE MVC routing: it operates on the raw path,
// so its order relative to UseRouting isn't critical, but it must run
// before UseAuthorization (in case a future policy needs ITenantContext
// already populated).
app.UseTenantResolution();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("PerIpPolicy");

app.Run();

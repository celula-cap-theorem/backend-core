using System.Text.RegularExpressions;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace cap_theorem_backend.Middleware;

/// <summary>
/// Intercepts routes shaped like /api/{cell}/{tenant}/... , validates the
/// slug, queries (with in-memory caching) the catalog to resolve the
/// physical connection string, and populates ITenantContext for the rest
/// of the pipeline.
///
/// Control-plane routes (/api/auth/*, /api/admin/*) do NOT go through
/// here: they are explicitly excluded because they have no cell/tenant
/// in the path.
/// </summary>
public class TenantResolutionMiddleware
{
    // Strict slug: lowercase letters, digits and hyphens, 3-30 characters.
    // Prevents name collisions and path traversal attempts.
    private static readonly Regex SlugPattern = new("^[a-z0-9-]{3,30}$", RegexOptions.Compiled);

    private static readonly string[] ExcludedPrefixes = ["/api/auth", "/api/admin"];

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public TenantResolutionMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, ICatalogRepository catalogRepository, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        var isExcluded = ExcludedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || isExcluded)
        {
            await _next(context);
            return;
        }

        // Expected shape: /api/{cell}/{tenant}/{resource...}
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid route: expected /api/{cell}/{tenant}/..." });
            return;
        }

        var cellSlug = segments[1];
        var tenantSlug = segments[2];

        if (!SlugPattern.IsMatch(cellSlug) || !SlugPattern.IsMatch(tenantSlug))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid cell or tenant slug." });
            return;
        }

        var cacheKey = $"tenant-conn:{cellSlug}:{tenantSlug}";

        var connectionInfo = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await catalogRepository.ResolveTenantConnectionAsync(cellSlug, tenantSlug);
        });

        if (connectionInfo is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Cell or tenant not found." });
            return;
        }

        if (!connectionInfo.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "The tenant is paused (TTL expired or suspended)." });
            return;
        }

        tenantContext.Set(connectionInfo.TenantId, cellSlug, tenantSlug, connectionInfo.ConnectionString);

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}

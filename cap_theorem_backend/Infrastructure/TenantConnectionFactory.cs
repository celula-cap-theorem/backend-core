using System.Data;
using Microsoft.Data.SqlClient;

namespace cap_theorem_backend.Infrastructure;

/// <summary>
/// Single entry point to TENANT databases (data plane). Data-plane
/// repositories (e.g. IBookingRepository) never open a SqlConnection
/// directly: they always request one from this factory, which reads the
/// connection string already resolved on ITenantContext.
/// </summary>
public interface ITenantConnectionFactory
{
    IDbConnection GetConnection();
}

public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly ITenantContext _tenantContext;

    public TenantConnectionFactory(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public IDbConnection GetConnection()
    {
        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException(
                "No tenant has been resolved for this request. " +
                "Is TenantResolutionMiddleware missing from the pipeline, " +
                "or does the route not include {cell}/{tenant}?");
        }

        return new SqlConnection(_tenantContext.ConnectionString);
    }
}

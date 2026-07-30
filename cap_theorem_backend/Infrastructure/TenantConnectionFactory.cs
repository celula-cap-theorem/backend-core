using MySqlConnector;

namespace cap_theorem_backend.Infrastructure;

public interface ITenantConnectionFactory
{
    MySqlConnection GetConnection();
}

public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly ITenantContext _tenantContext;

    public TenantConnectionFactory(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public MySqlConnection GetConnection()
    {
        if (!_tenantContext.IsResolved)
        {
            throw new InvalidOperationException(
                "No tenant has been resolved for this request. " +
                "Is TenantResolutionMiddleware missing from the pipeline, " +
                "or does the route not include {cell}/{tenant}?");
        }

        return new MySqlConnection(_tenantContext.ConnectionString);
    }
}

namespace cap_theorem_backend.Infrastructure;

/// <summary>
/// Scoped service (one instance per HTTP request) carrying the cell/tenant
/// identity resolved by TenantResolutionMiddleware. The rest of the
/// pipeline (controllers, data-plane repositories) reads from here to
/// know which physical database to operate against.
/// </summary>
public interface ITenantContext
{
    int TenantId { get; }
    string CellSlug { get; }
    string TenantSlug { get; }
    string ConnectionString { get; }
    bool IsResolved { get; }

    void Set(int tenantId, string cellSlug, string tenantSlug, string connectionString);
}

public class TenantContext : ITenantContext
{
    public int TenantId { get; private set; }
    public string CellSlug { get; private set; } = string.Empty;
    public string TenantSlug { get; private set; } = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    public void Set(int tenantId, string cellSlug, string tenantSlug, string connectionString)
    {
        TenantId = tenantId;
        CellSlug = cellSlug;
        TenantSlug = tenantSlug;
        ConnectionString = connectionString;
        IsResolved = true;
    }
}

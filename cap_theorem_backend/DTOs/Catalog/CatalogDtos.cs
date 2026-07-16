namespace cap_theorem_backend.DTOs.Catalog;

/// <summary>A registered cell within the catalog (control plane).</summary>
public record CellDto(
    int Id,
    string Name,
    DateTime CreatedAt
);

/// <summary>
/// A tenant (physical database provisioned) within a cell.
/// Does not include the plaintext connection string: that is only
/// resolved internally by the backend, never exposed to the client.
/// </summary>
public record TenantDto(
    int Id,
    int CellId,
    string Slug,
    long MaxSizeBytes,
    long CurrentSizeBytes,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsActive
);

public record CreateCellRequest(string Name);

public record CreateTenantRequest(int CellId, string Slug);

/// <summary>
/// Internal result of resolving cell+tenant to a physical connection.
/// This DTO never leaves the backend over HTTP; it only flows between
/// the middleware, ITenantContext and ITenantConnectionFactory.
/// </summary>
public record TenantConnectionInfo(
    int TenantId,
    string ConnectionString,
    bool IsActive
);

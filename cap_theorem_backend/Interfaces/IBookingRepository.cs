using cap_theorem_backend.DTOs.Bookings;

namespace cap_theorem_backend.Interfaces;

/// <summary>
/// Example of the ITenantRepository family: its concrete implementation
/// ONLY invokes stored procedures/views on the TENANT database resolved
/// for the current request (never the catalog). Zero business logic,
/// zero dynamic SQL.
///
/// This is an illustrative example (Booking). Any real business resource
/// exposed by a cell follows the same pattern: an interface + an
/// implementation that calls the matching stored procedure via
/// ITenantConnectionFactory.
/// </summary>
public interface IBookingRepository
{
    Task<BookingDto> CreateAsync(CreateBookingRequest request);
    Task<IEnumerable<BookingDto>> ListAsync();
}

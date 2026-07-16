using System.Data;
using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Bookings;
using cap_theorem_backend.Infrastructure;
using Dapper;

namespace cap_theorem_backend.Repositories;

/// <summary>
/// Illustrative example of a DATA-PLANE repository. Never opens its own
/// connection: always requests one from ITenantConnectionFactory, which
/// already points to the correct tenant database for the current request.
/// Only invokes stored procedures, never builds dynamic SQL.
/// </summary>
public class BookingRepository : IBookingRepository
{
    private readonly ITenantConnectionFactory _connectionFactory;

    public BookingRepository(ITenantConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BookingDto> CreateAsync(CreateBookingRequest request)
    {
        using var connection = _connectionFactory.GetConnection();
        return await connection.QuerySingleAsync<BookingDto>(
            "sp_CreateBooking",
            new { request.Date, request.CustomerId, request.ResourceId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<BookingDto>> ListAsync()
    {
        using var connection = _connectionFactory.GetConnection();
        return await connection.QueryAsync<BookingDto>(
            "sp_ListBookings",
            commandType: CommandType.StoredProcedure);
    }
}

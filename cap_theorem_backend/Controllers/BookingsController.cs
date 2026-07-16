using cap_theorem_backend.Interfaces;
using cap_theorem_backend.DTOs.Bookings;
using cap_theorem_backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace cap_theorem_backend.Controllers;

/// <summary>
/// Illustrative example of a business resource owned by a tenant. The
/// route includes {cell}/{tenant} so TenantResolutionMiddleware has
/// already populated ITenantContext before reaching here. Zero business
/// logic: it only delegates to IBookingRepository (which already points
/// to the correct tenant database) after validating that the token
/// belongs to this tenant.
/// </summary>
[ApiController]
[Route("api/{cell}/{tenant}/bookings")]
public class BookingsController : TenantScopedControllerBase
{
    private readonly IBookingRepository _bookingRepository;

    public BookingsController(IBookingRepository bookingRepository, ITenantContext tenantContext)
        : base(tenantContext)
    {
        _bookingRepository = bookingRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> List()
    {
        var forbidden = ValidateTenantFromToken();
        if (forbidden is not null) return forbidden;

        var bookings = await _bookingRepository.ListAsync();
        return Ok(bookings);
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest request)
    {
        var forbidden = ValidateTenantFromToken();
        if (forbidden is not null) return forbidden;

        var booking = await _bookingRepository.CreateAsync(request);
        return CreatedAtAction(nameof(List), booking);
    }
}

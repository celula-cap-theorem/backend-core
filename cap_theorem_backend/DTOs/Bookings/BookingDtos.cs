namespace cap_theorem_backend.DTOs.Bookings;

/// <summary>
/// Illustrative example of a business resource that lives INSIDE a tenant
/// database. The backend knows nothing about "bookings" as a concept:
/// it only invokes the corresponding stored procedure and maps rows to
/// this DTO.
/// </summary>
public record BookingDto(
    int Id,
    DateTime Date,
    int CustomerId,
    int ResourceId
);

public record CreateBookingRequest(DateTime Date, int CustomerId, int ResourceId);

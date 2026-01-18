namespace Travel.Api.Dtos;

public record BookingStatsDto(
    int TotalBookings,
    int PendingPayment,
    int Confirmed,
    int Cancelled,
    int Expired,
    decimal TotalRevenue,
    decimal PendingRevenue,
    Dictionary<string, int> BookingsByStatus,
    Dictionary<string, decimal> RevenueByStatus
);

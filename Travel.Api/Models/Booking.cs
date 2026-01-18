using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public enum BookingStatus { PendingPayment, Confirmed, Cancelled, Expired }

public class BookingContact
{
    [BsonElement("fullName")] public string FullName { get; set; } = default!;
    [BsonElement("email")] public string Email { get; set; } = default!;
    [BsonElement("phone")] public string? Phone { get; set; }
    [BsonElement("country")] public string? Country { get; set; }
}

public class BookingGuest
{
    [BsonElement("fullName")] public string FullName { get; set; } = default!;
    [BsonElement("age")] public int? Age { get; set; }
    [BsonElement("passportNo")] public string? PassportNo { get; set; }
}

public class BookingPricing
{
    [BsonElement("currency")] public string Currency { get; set; } = "USD";
    [BsonElement("subtotal")] public decimal Subtotal { get; set; }
    [BsonElement("discount")] public decimal Discount { get; set; }
    [BsonElement("tax")] public decimal Tax { get; set; }
    [BsonElement("total")] public decimal Total { get; set; }
}

public class Booking
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("bookingCode")] public string BookingCode { get; set; } = default!;

    [BsonElement("tourId")] public ObjectId TourId { get; set; }
    [BsonElement("tourDateId")] public ObjectId? TourDateId { get; set; } // group departure
    [BsonElement("travelDate")] public DateTime? TravelDate { get; set; } // private tour date

    // store as string for flexibility: "Private" | "Group"
    [BsonElement("tourType")] public string TourType { get; set; } = default!;

    [BsonElement("contact")] public BookingContact Contact { get; set; } = new();
    [BsonElement("guests")] public List<BookingGuest> Guests { get; set; } = new();

    [BsonElement("specialRequests")] public string? SpecialRequests { get; set; }
    [BsonElement("pricing")] public BookingPricing Pricing { get; set; } = new();

    [BsonElement("status")] public BookingStatus Status { get; set; } = BookingStatus.PendingPayment;

    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public enum PaymentStatus { Created, Pending, Paid, Failed, Expired, Refunded }

public class Payment
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("bookingId")] public ObjectId BookingId { get; set; }

    [BsonElement("provider")] public string Provider { get; set; } = "manual"; // qpay/stripe/etc
    [BsonElement("invoiceId")] public string? InvoiceId { get; set; }

    [BsonElement("amount")] public decimal Amount { get; set; }
    [BsonElement("currency")] public string Currency { get; set; } = "USD";

    [BsonElement("status")] public PaymentStatus Status { get; set; } = PaymentStatus.Created;

    [BsonElement("providerCheckoutUrl")] public string? ProviderCheckoutUrl { get; set; }
    [BsonElement("providerQrText")] public string? ProviderQrText { get; set; }

    [BsonElement("providerRaw")] public BsonDocument? ProviderRaw { get; set; }

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

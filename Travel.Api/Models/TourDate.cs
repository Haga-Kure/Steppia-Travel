using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public enum TourDateStatus { Open, SoldOut, Closed }

public class TourDate
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("tourId")] public ObjectId TourId { get; set; }
    [BsonElement("startDate")] public DateTime StartDate { get; set; }
    [BsonElement("endDate")] public DateTime EndDate { get; set; }

    [BsonElement("capacity")] public int Capacity { get; set; }
    [BsonElement("priceOverride")] public decimal? PriceOverride { get; set; }

    [BsonElement("status")] public TourDateStatus Status { get; set; } = TourDateStatus.Open;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

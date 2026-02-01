using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public class PendingRegistration
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("email")] public string Email { get; set; } = default!;
    [BsonElement("firstName")] public string FirstName { get; set; } = default!;
    [BsonElement("lastName")] public string LastName { get; set; } = default!;
    [BsonElement("phone")] public string? Phone { get; set; }
    [BsonElement("passwordHash")] public string PasswordHash { get; set; } = default!;
    [BsonElement("code")] public string Code { get; set; } = default!; // 6-digit string
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public class User
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("email")] public string Email { get; set; } = default!;
    [BsonElement("fullName")] public string FullName { get; set; } = default!;
    [BsonElement("phone")] public string? Phone { get; set; }
    [BsonElement("passwordHash")] public string PasswordHash { get; set; } = default!;
    [BsonElement("role")] public string Role { get; set; } = "user";
    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("lastLoginAt")] public DateTime? LastLoginAt { get; set; }
}

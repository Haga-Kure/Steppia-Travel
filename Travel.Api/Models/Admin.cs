using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public class Admin
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("username")] public string Username { get; set; } = default!;
    [BsonElement("passwordHash")] public string PasswordHash { get; set; } = default!;
    [BsonElement("email")] public string? Email { get; set; }
    [BsonElement("role")] public string Role { get; set; } = "admin";
    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [BsonElement("lastLoginAt")] public DateTime? LastLoginAt { get; set; }
}

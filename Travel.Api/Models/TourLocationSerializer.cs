using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Travel.Api.Models;

/// <summary>
/// Deserializes locations from either legacy string array ["Ulaanbaatar", "Khovd"]
/// or new format [{ name, latitude, longitude }, ...].
/// </summary>
public class TourLocationListSerializer : SerializerBase<List<TourLocation>>
{
    private static readonly BsonDocumentSerializer DocumentSerializer = new();

    private static string? GetLocationCoordString(BsonDocument doc, string key)
    {
        if (!doc.Contains(key)) return null;
        var v = doc[key];
        return v.BsonType switch
        {
            BsonType.String => v.AsString,
            BsonType.Double => v.AsDouble.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BsonType.Int32 => v.AsInt32.ToString(),
            BsonType.Int64 => v.AsInt64.ToString(),
            _ => null
        };
    }

    public override List<TourLocation> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        if (reader.GetCurrentBsonType() == BsonType.Null)
        {
            reader.ReadNull();
            return new List<TourLocation>();
        }
        if (reader.GetCurrentBsonType() != BsonType.Array)
        {
            reader.SkipValue();
            return new List<TourLocation>();
        }

        reader.ReadStartArray();
        var list = new List<TourLocation>();
        while (reader.ReadBsonType() != BsonType.EndOfDocument)
        {
            switch (reader.CurrentBsonType)
            {
                case BsonType.String:
                    list.Add(new TourLocation { Name = reader.ReadString() ?? "", Latitude = null, Longitude = null });
                    break;
                case BsonType.Document:
                    var doc = DocumentSerializer.Deserialize(context, args);
                    var name = doc.GetValue("name", BsonString.Empty).AsString ?? "";
                    var lat = GetLocationCoordString(doc, "latitude");
                    var lon = GetLocationCoordString(doc, "longitude");
                    list.Add(new TourLocation { Name = name ?? "", Latitude = lat, Longitude = lon });
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndArray();
        return list;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, List<TourLocation> value)
    {
        if (value == null)
        {
            context.Writer.WriteNull();
            return;
        }
        var writer = context.Writer;
        writer.WriteStartArray();
        foreach (var loc in value)
        {
            writer.WriteStartDocument();
            writer.WriteName("name");
            writer.WriteString(loc.Name ?? "");
            writer.WriteName("latitude");
            writer.WriteString(loc.Latitude ?? "");
            writer.WriteName("longitude");
            writer.WriteString(loc.Longitude ?? "");
            writer.WriteEndDocument();
        }
        writer.WriteEndArray();
    }
}

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NamedPipeSync.Common.Domain;

namespace NamedPipeSync.Common.Infrastructure.Protocol;

/// <summary>
/// System.Text.Json converter for the domain Checkpoint value object.
/// Keeps JSON-specific logic in Infrastructure and avoids adding any
/// serialization attributes or dependencies to the Domain layer.
/// </summary>
internal sealed class CheckpointJsonConverter : JsonConverter<Checkpoint>
{
    public override Checkpoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject for Checkpoint");
        }

        int id = 0;
        double x = 0;
        double y = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                // construct from accumulated fields
                return new Checkpoint(id, new Coordinate(x, y));
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name in Checkpoint object");
            }

            string? propName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end when reading Checkpoint property value");
            }

            switch (propName)
            {
                case "id":
                    id = reader.TokenType switch
                    {
                        JsonTokenType.Number => reader.TryGetInt32(out var iid) ? iid : throw new JsonException("Invalid id"),
                        JsonTokenType.String when int.TryParse(reader.GetString(), out var iid) => iid,
                        _ => throw new JsonException("Invalid id token type")
                    };
                    break;

                case "location":
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw new JsonException("Expected StartObject for location");
                    }

                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                        {
                            break;
                        }

                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            throw new JsonException("Expected property name in location object");
                        }

                        var locProp = reader.GetString();
                        if (!reader.Read())
                        {
                            throw new JsonException("Unexpected end when reading location value");
                        }

                        switch (locProp)
                        {
                            case "x":
                                x = reader.TokenType == JsonTokenType.Number
                                    ? reader.GetDouble()
                                    : double.TryParse(reader.GetString(), out var xv) ? xv : throw new JsonException("Invalid x");
                                break;
                            case "y":
                                y = reader.TokenType == JsonTokenType.Number
                                    ? reader.GetDouble()
                                    : double.TryParse(reader.GetString(), out var yv) ? yv : throw new JsonException("Invalid y");
                                break;
                            default:
                                reader.Skip();
                                break;
                        }
                    }
                    break;

                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON while reading Checkpoint");
    }

    public override void Write(Utf8JsonWriter writer, Checkpoint value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("id", value.Id);
        writer.WritePropertyName("location");
        writer.WriteStartObject();
        writer.WriteNumber("x", value.Location.X);
        writer.WriteNumber("y", value.Location.Y);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

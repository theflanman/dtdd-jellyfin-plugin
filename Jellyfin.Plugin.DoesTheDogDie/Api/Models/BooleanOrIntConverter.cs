using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.DoesTheDogDie.Api.Models;

/// <summary>
/// JSON converter that handles both boolean values and integer values (0/1).
/// Some API endpoints return booleans while others return integers.
/// </summary>
public class BooleanOrIntConverter : JsonConverter<bool>
{
    /// <inheritdoc />
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetInt32() != 0,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to boolean")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Deserializes nullable GUIDs leniently — invalid strings (e.g. FE pathology slugs) become null
/// instead of failing the entire promote request body.
/// </summary>
public sealed class NullableGuidLenientJsonConverter : JsonConverter<Guid?>
{
    public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => Guid.TryParse(reader.GetString(), out var guid) ? guid : null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing Guid?."),
        };
    }

    public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

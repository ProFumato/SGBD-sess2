using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadelCourtManagement.Api;

public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] AcceptedFormats =
    [
        "HH:mm",
        "HH:mm:ss",
        "HH:mm:ss.FFFFFFF"
    ];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String
            || !TimeOnly.TryParseExact(
                reader.GetString(),
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var value))
        {
            throw new JsonException("Time values must use HH:mm, HH:mm:ss, or HH:mm:ss.FFFFFFF format.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}

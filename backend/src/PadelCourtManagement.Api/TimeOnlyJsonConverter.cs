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
        "HH:mm:ss.FFFFFFF",
        "HH:mm:ss.fffffff",
        "HH:mm:ss.ffffff",
        "HH:mm:ss.fffff",
        "HH:mm:ss.ffff",
        "HH:mm:ss.fff",
        "HH:mm:ss.ff",
        "HH:mm:ss.f"
    ];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for TimeOnly.");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new JsonException("The JSON value is not in a supported TimeOnly format.");
        }

        var value = raw.Trim();

        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
        {
            return timeOnly;
        }

        if (TimeOnly.TryParseExact(value, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnly))
        {
            return timeOnly;
        }

        if (value.EndsWith("Z", StringComparison.Ordinal))
        {
            var withoutUtcMarker = value[..^1];
            if (TimeOnly.TryParse(withoutUtcMarker, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnly))
            {
                return timeOnly;
            }
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var offsetValue))
        {
            return TimeOnly.FromDateTime(offsetValue.DateTime);
        }

        throw new JsonException("The JSON value is not in a supported TimeOnly format.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }
}

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
        "HH:mm:ss.f",
        "HH:mm:ssZ",
        "HH:mm:ss+00:00",
        "HH:mm:ss-00:00",
        "HH:mmZ",
        "HH:mm+00:00",
        "HH:mm-00:00"
    ];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a JSON string for TimeOnly.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("The JSON value is not in a supported TimeOnly format.");
        }

        var normalized = value.Trim();
        var timezoneNormalised = normalized.EndsWith("Z", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;

        if (TimeOnly.TryParse(timezoneNormalised, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
        {
            return timeOnly;
        }

        if (TimeOnly.TryParseExact(timezoneNormalised, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out timeOnly))
        {
            return timeOnly;
        }

        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var offsetValue))
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

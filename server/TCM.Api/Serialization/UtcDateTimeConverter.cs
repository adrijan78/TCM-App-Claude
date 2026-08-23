using System.Text.Json;
using System.Text.Json.Serialization;

namespace TCM.Api.Serialization;

/// <summary>
/// Writes every <see cref="DateTime"/> as an explicit UTC instant, and reads one back the same
/// way.
/// </summary>
/// <remarks>
/// Every timestamp in this application is UTC (see the note on <c>Training.Date</c> — EF Core 10
/// cannot translate <c>DateTimeOffset.Year</c> in a <c>GroupBy</c>, so the columns are plain
/// <c>datetime2</c>). EF hands those back with <see cref="DateTimeKind.Unspecified"/>, and the
/// default serializer then writes <c>"2026-08-25T16:00:00"</c> with no zone marker at all.
///
/// JavaScript parses a naive string like that as <em>local</em> time, so a session stored at
/// 16:00 UTC rendered as 16:00 in a UTC+2 browser: every training an hour or two early, and the
/// dashboard countdown short by the same amount. Appending the <c>Z</c> is what makes the wire
/// format say what the values have always meant.
/// </remarks>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // No marker on the way in either. The API's contract is UTC, so take it at its word
            // rather than reinterpreting it against the server's own time zone.
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
    }
}

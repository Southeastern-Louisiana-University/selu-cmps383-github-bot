using System;
using Newtonsoft.Json;

namespace Selu383Bot.Features.RateLimits;

public class ResetConverter : JsonConverter<DateTimeOffset>
{
    public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        writer.WriteValue(TimeZoneInfo.ConvertTime(value, tz).ToString("yyyy-MM-dd hh:mm:ss tt zz"));
    }

    public override DateTimeOffset ReadJson(JsonReader reader, Type objectType, DateTimeOffset existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return DateTimeOffset.UnixEpoch.Add(TimeSpan.FromSeconds(reader.Value as long? ?? 0)).ToUniversalTime();
    }
}

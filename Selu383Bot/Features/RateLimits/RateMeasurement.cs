using System;
using Newtonsoft.Json;

namespace Selu383Bot.Features.RateLimits;

public class RateMeasurement
{
    public int Limit { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }

    [JsonConverter(typeof(ResetConverter))]
    public DateTimeOffset Reset { get; set; }
}

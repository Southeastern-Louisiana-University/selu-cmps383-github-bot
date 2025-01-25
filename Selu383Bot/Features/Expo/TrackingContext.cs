using System.Text.Json.Serialization;

namespace Selu383Bot.Features.Expo;

public class TrackingContext
{
    [JsonPropertyName("no_wait")]
    public bool NoWait { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; }

    [JsonPropertyName("dev_client")]
    public bool DevClient { get; set; }

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; }

    [JsonPropertyName("run_from_ci")]
    public bool RunFromCi { get; set; }

    [JsonPropertyName("tracking_id")]
    public string TrackingId { get; set; }

    [JsonPropertyName("project_type")]
    public string ProjectType { get; set; }
}

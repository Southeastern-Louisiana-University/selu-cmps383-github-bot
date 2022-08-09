using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Webhook;

public class EventPayload
{
    [JsonProperty("repository")]
    public Repository Repository { get; set; }

    [JsonProperty("organization")]
    public Organization Organization { get; set; }

    [JsonProperty("check_suite")]
    public CheckSuite CheckSuite { get; set; }

    [JsonProperty("team")]
    public Team Team { get; set; }
}

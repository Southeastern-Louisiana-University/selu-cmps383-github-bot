using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.CommitStatuses;

public class CommitStatus
{
    [JsonProperty("state")]
    public string State { get; set; } = "failure";

    [JsonProperty("target_url")]
    public string TargetUrl { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("context")]
    public string Context { get; set; } = "Selu383Bot";
}
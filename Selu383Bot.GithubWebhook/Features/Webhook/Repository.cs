using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Webhook;

public class Repository
{
    public ulong Id { get; set; }

    [JsonProperty("full_name")]
    public string FullName { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

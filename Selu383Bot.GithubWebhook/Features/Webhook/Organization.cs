using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Webhook;

public class Organization
{
    public ulong Id { get; set; }

    [JsonProperty("login")]
    public string Login { get; set; }
}

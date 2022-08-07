using Newtonsoft.Json.Linq;

namespace Selu383Bot.GithubWebhook.Features.Webhook;

public class Event
{
    public string Action { get; set; }
    public string Scope { get; set; }

    public string TargetType { get; set; }
    public JObject Target { get; set; }

    public EventPayload Payload { get; set; }
}

using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Webhook;

public class CheckSuite
{
    [JsonProperty("conclusion")]
    public string Conclusion { get; set; }
        
    /// <summary>
    /// The commit hash
    /// </summary>
    [JsonProperty("after")]
    public string After { get; set; }
}
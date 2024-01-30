using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Secrets;

public class ActionsPublicKey
{
    [JsonProperty("key_id")]
    public string KeyId { get; set; }

    [JsonProperty("key")]
    public string Key { get; set; }

    [JsonIgnore]
    public byte[] KeyBytes { get; set; }
}

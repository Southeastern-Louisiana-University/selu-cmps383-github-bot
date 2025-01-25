using Newtonsoft.Json;

namespace Selu383Bot.Features.Secrets;

public class CreateRepositorySecret
{
    [JsonProperty("encrypted_value")]
    public string EncryptedValue { get; set; }

    [JsonProperty("key_id")]
    public string KeyId { get; set; }
}

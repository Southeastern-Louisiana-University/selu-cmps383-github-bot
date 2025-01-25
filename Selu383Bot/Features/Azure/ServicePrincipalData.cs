using System.Text.Json.Serialization;

namespace Selu383Bot.Features.Azure;

public class ServicePrincipalData
{
    [JsonPropertyName("appId")]
    public string AppId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("tenant")]
    public string Tenant { get; set; }

    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; }
}

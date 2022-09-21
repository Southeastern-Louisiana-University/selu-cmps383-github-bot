using System;
using System.Text.Json.Serialization;

namespace Selu383Bot.GithubWebhook.Features.Expo;

public class Build
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("appId")]
    public string AppId { get; set; }

    [JsonPropertyName("initiatingUserId")]
    public string InitiatingUserId { get; set; }

    [JsonPropertyName("cancelingUserId")]
    public object CancelingUserId { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("artifacts")]
    public Artifacts Artifacts { get; set; }

    [JsonPropertyName("metadata")]
    public Metadata Metadata { get; set; }

    [JsonPropertyName("metrics")]
    public Metrics Metrics { get; set; }

    [JsonPropertyName("error")]
    public object Error { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("expirationDate")]
    public DateTime ExpirationDate { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; }
}
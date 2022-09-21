using System.Text.Json.Serialization;

namespace Selu383Bot.GithubWebhook.Features.Expo;

public class Artifacts
{
    [JsonPropertyName("logsS3KeyPrefix")]
    public string LogsS3KeyPrefix { get; set; }

    [JsonPropertyName("applicationArchiveUrl")]
    public string ApplicationArchiveUrl { get; set; }

    [JsonPropertyName("buildUrl")]
    public string BuildUrl { get; set; }
}
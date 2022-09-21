using System.Text.Json.Serialization;

namespace Selu383Bot.GithubWebhook.Features.Expo;

public class Metrics
{
    [JsonPropertyName("memory")]
    public long Memory { get; set; }

    [JsonPropertyName("buildEndTimestamp")]
    public long BuildEndTimestamp { get; set; }

    [JsonPropertyName("totalDiskReadBytes")]
    public int TotalDiskReadBytes { get; set; }

    [JsonPropertyName("buildStartTimestamp")]
    public long BuildStartTimestamp { get; set; }

    [JsonPropertyName("totalDiskWriteBytes")]
    public int TotalDiskWriteBytes { get; set; }

    [JsonPropertyName("cpuActiveMilliseconds")]
    public double CpuActiveMilliseconds { get; set; }

    [JsonPropertyName("buildEnqueuedTimestamp")]
    public long BuildEnqueuedTimestamp { get; set; }

    [JsonPropertyName("totalNetworkEgressBytes")]
    public int TotalNetworkEgressBytes { get; set; }

    [JsonPropertyName("totalNetworkIngressBytes")]
    public int TotalNetworkIngressBytes { get; set; }
}
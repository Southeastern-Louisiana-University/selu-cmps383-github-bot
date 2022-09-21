using System.Text.Json.Serialization;

namespace Selu383Bot.GithubWebhook.Features.Expo;

public class Metadata
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; }

    [JsonPropertyName("workflow")]
    public string Workflow { get; set; }

    [JsonPropertyName("runFromCI")]
    public bool RunFromCI { get; set; }

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; }

    [JsonPropertyName("cliVersion")]
    public string CliVersion { get; set; }

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; }

    [JsonPropertyName("buildProfile")]
    public string BuildProfile { get; set; }

    [JsonPropertyName("distribution")]
    public string Distribution { get; set; }

    [JsonPropertyName("appIdentifier")]
    public string AppIdentifier { get; set; }

    [JsonPropertyName("gitCommitHash")]
    public string GitCommitHash { get; set; }

    [JsonPropertyName("appBuildVersion")]
    public string AppBuildVersion { get; set; }

    [JsonPropertyName("trackingContext")]
    public TrackingContext TrackingContext { get; set; }

    [JsonPropertyName("gitCommitMessage")]
    public string GitCommitMessage { get; set; }

    [JsonPropertyName("credentialsSource")]
    public string CredentialsSource { get; set; }

    [JsonPropertyName("runWithNoWaitFlag")]
    public bool RunWithNoWaitFlag { get; set; }

    [JsonPropertyName("reactNativeVersion")]
    public string ReactNativeVersion { get; set; }

    [JsonPropertyName("isGitWorkingTreeDirty")]
    public bool IsGitWorkingTreeDirty { get; set; }
}
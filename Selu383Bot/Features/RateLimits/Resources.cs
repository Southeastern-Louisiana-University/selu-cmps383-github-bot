namespace Selu383Bot.Features.RateLimits;

public class Resources
{
    public RateMeasurement Core { get; set; }
    public RateMeasurement Search { get; set; }
    public RateMeasurement Graphql { get; set; }
    public RateMeasurement IntegrationManifest { get; set; }
    public RateMeasurement SourceImport { get; set; }
    public RateMeasurement CodeScanningUpload { get; set; }
    public RateMeasurement ActionsRunnerRegistration { get; set; }
    public RateMeasurement Scim { get; set; }
    public RateMeasurement DependencySnapshots { get; set; }
}

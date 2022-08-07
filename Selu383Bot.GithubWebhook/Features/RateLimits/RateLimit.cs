namespace Selu383Bot.GithubWebhook.Features.RateLimits;

public class RateLimit
{
    public Resources Resources { get; set; }
    public RateMeasurement Rate { get; set; }
}

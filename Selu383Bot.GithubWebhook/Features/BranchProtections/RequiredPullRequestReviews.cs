using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.BranchProtections;

public class RequiredPullRequestReviews
{
    [JsonProperty("dismissal_restrictions")]
    public DismissalRestrictions DismissalRestrictions { get; set; } = new();

    [JsonProperty("dismiss_stale_reviews")]
    public bool DismissStaleReviews { get; set; }

    [JsonProperty("require_code_owner_reviews")]
    public bool RequireCodeOwnerReviews { get; set; }

    [JsonProperty("required_approving_review_count")]
    public int RequiredApprovingReviewCount { get; set; }

    [JsonProperty("bypass_pull_request_allowances")]
    public BypassPullRequestAllowances BypassPullRequestAllowances { get; set; } = new BypassPullRequestAllowances();
}
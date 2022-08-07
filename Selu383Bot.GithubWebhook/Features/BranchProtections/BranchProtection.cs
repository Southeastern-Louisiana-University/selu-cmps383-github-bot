using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.BranchProtections;

public class BranchProtection
{
	[JsonProperty("required_status_checks")]
	public RequiredStatusChecks RequiredStatusChecks { get; set; }

	[JsonProperty("enforce_admins")]
	public bool EnforceAdmins { get; set; }

	[JsonProperty("required_pull_request_reviews")]
	public RequiredPullRequestReviews RequiredPullRequestReviews { get; set; }

	[JsonProperty("restrictions")]
	public Restrictions Restrictions { get; set; }

	[JsonProperty("required_linear_history")]
	public bool RequiredLinearHistory { get; set; }

	[JsonProperty("allow_force_pushes")]
	public bool AllowForcePushes { get; set; }

	[JsonProperty("allow_deletions")]
	public bool AllowDeletions { get; set; }

	[JsonProperty("block_creations")]
	public bool BlockCreations { get; set; }

	[JsonProperty("required_conversation_resolution")]
	public bool RequiredConversationResolution { get; set; }
}

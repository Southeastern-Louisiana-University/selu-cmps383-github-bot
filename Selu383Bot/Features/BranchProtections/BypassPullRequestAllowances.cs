using System.Collections.Generic;
using Newtonsoft.Json;

namespace Selu383Bot.Features.BranchProtections;

public class BypassPullRequestAllowances
{
    [JsonProperty("users")]
    public List<string> Users { get; set; } = new List<string>();

    [JsonProperty("teams")]
    public List<string> Teams { get; set; } = new List<string>();
}

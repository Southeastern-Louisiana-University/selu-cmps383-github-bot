using System.Collections.Generic;
using Newtonsoft.Json;

namespace Selu383Bot.Features.BranchProtections;

public class Restrictions
{
    [JsonProperty("users")]
    public List<string> Users { get; set; } = new List<string>();

    [JsonProperty("teams")]
    public List<string> Teams { get; set; } = new List<string>();

    [JsonProperty("apps")]
    public List<string> Apps { get; set; } = new List<string>();
}

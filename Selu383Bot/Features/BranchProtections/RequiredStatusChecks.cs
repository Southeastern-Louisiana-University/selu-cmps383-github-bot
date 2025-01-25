using System.Collections.Generic;
using Newtonsoft.Json;

namespace Selu383Bot.Features.BranchProtections;

public class RequiredStatusChecks
{
    [JsonProperty("strict")]
    public bool Strict { get; set; }

    [JsonProperty("contexts")]
    public List<string> Contexts { get; set; } = new List<string>();
}

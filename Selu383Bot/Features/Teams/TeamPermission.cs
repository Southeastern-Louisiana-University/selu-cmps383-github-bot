using Newtonsoft.Json;

namespace Selu383Bot.Features.Teams;

public class TeamPermission
{
    [JsonProperty("permission")]
    public string Permission { get; set; }
}

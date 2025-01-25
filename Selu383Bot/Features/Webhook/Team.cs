using Newtonsoft.Json;

namespace Selu383Bot.Features.Webhook;

public class Team
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("privacy")]
    public string Privacy { get; set; }
}

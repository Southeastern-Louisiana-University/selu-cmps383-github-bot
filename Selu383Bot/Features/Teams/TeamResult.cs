using System.Text.Json.Serialization;

namespace Selu383Bot.Features.Teams;

public class TeamResult
{
    [JsonPropertyName("permission")]
    public string Permission { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}

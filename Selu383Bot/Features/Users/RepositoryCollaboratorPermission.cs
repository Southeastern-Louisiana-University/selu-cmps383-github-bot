using Newtonsoft.Json;

namespace Selu383Bot.Features.Users;

public class RepositoryCollaboratorPermission
{
    [JsonProperty("permission")]
    public string Permission { get; set; }
}

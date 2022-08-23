using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Users;

public class RepositoryCollaboratorPermission
{
    [JsonProperty("permission")]
    public string Permission { get; set; }
}

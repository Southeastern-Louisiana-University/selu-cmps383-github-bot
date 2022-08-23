using Newtonsoft.Json;

namespace Selu383Bot.GithubWebhook.Features.Users;

public class UserDto
{
    [JsonProperty("login")]
    public string Login { get; set; }
}

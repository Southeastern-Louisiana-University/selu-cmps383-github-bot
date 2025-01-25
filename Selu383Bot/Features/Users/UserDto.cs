using Newtonsoft.Json;

namespace Selu383Bot.Features.Users;

public class UserDto
{
    [JsonProperty("login")]
    public string Login { get; set; }
}

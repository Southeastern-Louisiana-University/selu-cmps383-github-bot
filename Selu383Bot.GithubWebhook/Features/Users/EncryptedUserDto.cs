using System;

namespace Selu383Bot.GithubWebhook.Features.Users;

public class EncryptedUserDto
{
    public string Username { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

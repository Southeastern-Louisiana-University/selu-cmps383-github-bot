using System;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;
using Sodium;

namespace Selu383Bot.GithubWebhook.Extensions;

public static class HttpRequestExtensions
{
    public static void SetCookie(this HttpRequest req, string name, string value)
    {
        req.HttpContext.Response.Cookies.Append(name, value, new CookieOptions{HttpOnly = true, Secure = true, Expires = DateTimeOffset.UtcNow.AddDays(1)});
    }

    public static EncryptedUserDto GetAuthInfo(this HttpRequest req)
    {
        var authCookieValue = req.Cookies["auth"];
        if (authCookieValue == null)
        {
            return Unauthorized();
        }

        try
        {
            var split = authCookieValue.Split("|");
            var message = split[0];
            var nonce = Convert.FromHexString(split[1]);
            var key = Convert.FromHexString(FunctionHelper.GetEnvironmentVariable("SecretBoxKey"));
            var userData = JsonConvert.DeserializeObject<EncryptedUserDto>(Encoding.UTF8.GetString(SecretBox.Open(message, nonce, key)));

            if (userData == null || userData.CreatedUtc < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)))
            {
                return Unauthorized();
            }

            return userData;
        }
        catch (Exception)
        {
            return Unauthorized();
        }

        EncryptedUserDto Unauthorized()
        {
            return null;
        }
    }
}

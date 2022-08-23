using System;
using Microsoft.AspNetCore.Http;

namespace Selu383Bot.GithubWebhook.Extensions;

public static class HttpRequestExtensions
{
    public static void SetCookie(this HttpRequest req, string name, string value)
    {
        req.HttpContext.Response.Cookies.Append(name, value, new CookieOptions{HttpOnly = true, Secure = true, Expires = DateTimeOffset.UtcNow.AddDays(1)});
    }
}

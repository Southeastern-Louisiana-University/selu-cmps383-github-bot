using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using RestSharp;
using Selu383Bot.GithubWebhook.Extensions;
using Selu383Bot.GithubWebhook.Features.OAuth;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;
using Selu383Bot.GithubWebhook.Properties;
using Sodium;

namespace Selu383Bot.GithubWebhook.Functions;

public static class SetAzureEmail
{
    [FunctionName("StartLogin")]
    public static async Task<IActionResult> StartLogin([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "start-azure-login")] HttpRequest req)
    {
        var clientId = FunctionHelper.GetEnvironmentVariable("githubclientId");

        var state = Guid.NewGuid().ToString("N");
        using var hmac = GetHmac();
        var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes(state)));

        var url = new UriBuilder("https://github.com/login/oauth/authorize?"+AddQuery(new Dictionary<string,string>
        {
            {"client_id" , clientId},
            {"scope", "user:email"},
            {"redirect_uri", FunctionHelper.GetEnvironmentVariable("githubredirect")},
            {"state", computedHash},
            {"allow_signup","false"},
        }));

        req.SetCookie("state", state);

        return new RedirectResult(url.Uri.AbsoluteUri, false);
    }

    [FunctionName("FinishLogin")]
    public static async Task<IActionResult> FinishLogin([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "finish-azure-login")] HttpRequest req)
    {
        var cookieState = req.Cookies["state"];
        if (cookieState == null)
        {
            throw new Exception("No state");
        }

        using var hmac = GetHmac();
        var computedHash = hmac.ComputeHash(Encoding.ASCII.GetBytes(cookieState));
        var providedHash = Convert.FromHexString(req.Query["state"]);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, providedHash))
        {
            throw new Exception("Bad state");
        }

        var code = req.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new Exception("No code");
        }

        var clientId = FunctionHelper.GetEnvironmentVariable("githubclientId");
        var clientSecret = FunctionHelper.GetEnvironmentVariable("githubsecret");
        var githubOAuthClient = new RestClient("https://github.com");
        var userCodeRequest = new RestRequest("/login/oauth/access_token", Method.Post);
        userCodeRequest.AddBody(JsonConvert.SerializeObject(new
        {
            client_id = clientId,
            client_secret = clientSecret,
            code = code,
            redirect_uri = FunctionHelper.GetEnvironmentVariable("githubredirect")
        }), "application/json");
        var userCodeResponse = await githubOAuthClient.ExecuteAsync<AccessToken>(userCodeRequest);

        var data = userCodeResponse.Data;
        if (string.IsNullOrWhiteSpace(data?.access_token))
        {
            throw new Exception("No access_token");
        }

        var userGithubClient = new RestClient("https://api.github.com");
        userGithubClient.AddDefaultHeader("Authorization", $"token {data.access_token}");

        var userRequest = new RestRequest("/user", Method.Get);
        var userResponse = await userGithubClient.ExecuteAsync<UserDto>(userRequest);

        var username = userResponse.Data?.Login;
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new Exception("No username");
        }

        var key = Convert.FromHexString(FunctionHelper.GetEnvironmentVariable("SecretBoxKey"));
        var nonce = SecretBox.GenerateNonce();
        var message = Convert.ToHexString(SecretBox.Create(JsonConvert.SerializeObject(new EncryptedUserDto
        {
            Username = username,
            CreatedUtc = DateTimeOffset.UtcNow
        }), nonce, key));

        req.SetCookie("auth", message + "|" + Convert.ToHexString(nonce));

        return new RedirectResult("/api/set-email", false);
    }

    [FunctionName("set-email-get")]
    public static async Task<IActionResult> SetEmailGet([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "set-email")] HttpRequest req)
    {
        return new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = Resources.SubmitStuff
        };
    }

    [FunctionName("set-email-post")]
    public static async Task<IActionResult> SetEmailPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "set-email")] HttpRequest req)
    {
        var form = await req.ReadFormAsync();
        var email = form["email"];
        if (string.IsNullOrEmpty(email))
        {
            return new RedirectResult("/api/set-email", false);
        }

        return new ContentResult
        {
            ContentType = "text/plain",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = email
        };
    }

    private static string AddQuery(Dictionary<string,string> collection)
    {
        return string.Join("&", collection.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    private static HMACSHA256 GetHmac()
    {
        return new HMACSHA256(Convert.FromHexString(FunctionHelper.GetEnvironmentVariable("HmacSecret")));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using RestSharp;
using Selu383Bot.GithubWebhook.Extensions;
using Selu383Bot.GithubWebhook.Features.OAuth;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;
using Sodium;

namespace Selu383Bot.GithubWebhook.Functions;

public static class GithubLogin
{[FunctionName("start-github-login")]
    public static Task<IActionResult> StartLogin([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "start-github-login")] HttpRequest req)
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

        return Task.FromResult<IActionResult>(new RedirectResult(url.Uri.AbsoluteUri, false));
    }

    [FunctionName("finish-github-login")]
    public static async Task<IActionResult> FinishLogin([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "finish-github-login")] HttpRequest req)
    {
        var cookieState = req.Cookies["state"];
        if (cookieState == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "No state");
        }

        using var hmac = GetHmac();
        var computedHash = hmac.ComputeHash(Encoding.ASCII.GetBytes(cookieState));
        var providedHash = Convert.FromHexString(req.Query["state"]);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, providedHash))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Bad state");
        }

        var code = req.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "No code");
        }

        var clientId = FunctionHelper.GetEnvironmentVariable("githubclientId");
        var clientSecret = FunctionHelper.GetEnvironmentVariable("githubsecret");
        var githubOAuthClient = new RestClient("https://github.com");
        var userCodeRequest = new RestRequest("/login/oauth/access_token", Method.Post);
        userCodeRequest.AddBody(new
        {
            client_id = clientId,
            client_secret = clientSecret,
            code,
            redirect_uri = FunctionHelper.GetEnvironmentVariable("githubredirect")
        });
        var userCodeResponse = await githubOAuthClient.ExecuteAsync<AccessTokenResponse>(userCodeRequest);

        var data = userCodeResponse.Data;
        if (string.IsNullOrWhiteSpace(data?.access_token))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "No access_token");
        }

        var userGithubClient = new RestClient("https://api.github.com").UseSerializer(() => new JsonNetSerializer());
        userGithubClient.AddDefaultHeader("Authorization", $"token {data.access_token}");

        var userRequest = new RestRequest("/user");
        var userResponse = await userGithubClient.ExecuteAsync<UserDto>(userRequest);

        var username = userResponse.Data?.Login;
        if (string.IsNullOrWhiteSpace(username))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "No username");
        }

        var key = Convert.FromHexString(FunctionHelper.GetEnvironmentVariable("SecretBoxKey"));
        var nonce = SecretBox.GenerateNonce();
        var message = Convert.ToHexString(SecretBox.Create(JsonConvert.SerializeObject(new EncryptedUserDto
        {
            Username = username,
            CreatedUtc = DateTimeOffset.UtcNow,
        }), nonce, key));

        req.SetCookie("auth", message + "|" + Convert.ToHexString(nonce));

        var path = req.Cookies["path"];
        if (path == "secret")
        {
            return new RedirectResult("/api/set-secret", false);
        }

        return new RedirectResult("/api/set-email", false);
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

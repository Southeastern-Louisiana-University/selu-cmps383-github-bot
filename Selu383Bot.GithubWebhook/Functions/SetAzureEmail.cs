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
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "No access_token");
        }

        var userGithubClient = new RestClient("https://api.github.com");
        userGithubClient.AddDefaultHeader("Authorization", $"token {data.access_token}");

        var userRequest = new RestRequest("/user", Method.Get);
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
        var email = form["email"].ToString();
        if (string.IsNullOrEmpty(email))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing email");
        }

        var repository = form["repository"].ToString();
        if (string.IsNullOrEmpty(repository))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing repository");
        }

        if (!repository.Contains("cmps383"))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "You should only be using this for 383 :(");
        }

        var authCookieValue = req.Cookies["auth"];
        if (authCookieValue == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "No auth");
        }

        EncryptedUserDto userData;
        try
        {
            var split = authCookieValue.Split("|");
            var message = split[0];
            var nonce = Convert.FromHexString(split[1]);
            var key = Convert.FromHexString(FunctionHelper.GetEnvironmentVariable("SecretBoxKey"));
            userData = JsonConvert.DeserializeObject<EncryptedUserDto>(Encoding.UTF8.GetString(SecretBox.Open(message, nonce, key)));

            if (userData == null || userData.CreatedUtc < DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)))
            {
                return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "It has been over an hour - you'll need to login with github again.");
            }
        }
        catch (Exception e)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "Invalid auth");
        }

        var githubClient = new RestClient("https://api.github.com").UseSerializer(()=> new JsonNetSerializer());
        githubClient.AddDefaultHeader("Authorization", $"token {FunctionHelper.GetEnvironmentVariable("GithubAuthToken")}");

        var collaboratorRequest = new RestRequest("/repos/{owner}/{repo}/collaborators/{username}/permission");
        collaboratorRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        collaboratorRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));
        collaboratorRequest.AddParameter(Parameter.CreateParameter("username", userData.Username, ParameterType.UrlSegment));
        var collaboratorResult = await githubClient.ExecuteAsync<RepositoryCollaboratorPermission>(collaboratorRequest);
        // see: https://docs.github.com/en/rest/collaborators/collaborators#check-if-a-user-is-a-repository-collaborator
        var isCollaborator = collaboratorResult.StatusCode == HttpStatusCode.OK;
        if (!isCollaborator || collaboratorResult.Data == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "You don't have access to " + repository + " or you mistyped the repository name");
        }

        if (collaboratorResult.Data.Permission != "write" && collaboratorResult.Data.Permission != "admin")
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "You don't have access to '" + repository + "'. your access that we see is: " + collaboratorResult.Data.Permission);
        }

        var nameBase = repository.ToLowerInvariant();

        return new ContentResult
        {
            ContentType = "text/plain",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = nameBase
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

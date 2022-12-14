using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using RestSharp;
using Selu383Bot.GithubWebhook.Extensions;
using Selu383Bot.GithubWebhook.Features.Secrets;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;
using Selu383Bot.GithubWebhook.Properties;
using RestClient = RestSharp.RestClient;
using FluentAzure = Microsoft.Azure.Management.Fluent.Azure;

namespace Selu383Bot.GithubWebhook.Functions;

public static class SetPublishProfile
{
    [FunctionName("set-secret-get")]
    public static Task<IActionResult> SetSecretGet([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "set-secret")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "secret");
            return Task.FromResult<IActionResult>(new RedirectResult("/api/start-github-login", false));
        }

        return Task.FromResult<IActionResult>((new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = Resources.SetPublishProfile
        }));
    }

    [FunctionName("set-secret-post")]
    public static async Task<IActionResult> SetSecretPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "set-secret")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "secret");
            return new RedirectResult("/api/start-github-login", false);
        }

        var form = await req.ReadFormAsync();
        var repository = form["repository"].ToString().Trim();

        if (string.IsNullOrEmpty(repository))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing repository");
        }

        var file = form.Files.FirstOrDefault();
        if (file == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing file");
        }

        if (!repository.Contains("cmps383"))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "You should only be using this for 383 :(");
        }

        var githubClient = new RestClient("https://api.github.com").UseSerializer(() => new JsonNetSerializer());
        githubClient.AddDefaultHeader("Authorization", $"token {FunctionHelper.GetEnvironmentVariable("GithubAuthToken")}");

        var collaboratorRequest = new RestRequest("/repos/{owner}/{repo}/collaborators/{username}/permission");
        collaboratorRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        collaboratorRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));
        collaboratorRequest.AddParameter(Parameter.CreateParameter("username", userData.Username, ParameterType.UrlSegment));
        var collaboratorResult = await githubClient.ExecuteAsync<RepositoryCollaboratorPermission>(collaboratorRequest);
        // see: https://docs.github.com/en/rest/collaborators/collaborators#get-repository-permissions-for-a-user
        var isCollaborator = collaboratorResult.StatusCode == HttpStatusCode.OK;
        if (!isCollaborator || collaboratorResult.Data == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "You don't have access to " + repository + " or you mistyped the repository name");
        }

        if (collaboratorResult.Data.Permission != "write" && collaboratorResult.Data.Permission != "admin")
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.Unauthorized, "You don't have access to '" + repository + "'. your access that we see is: " + collaboratorResult.Data.Permission);
        }

        var secretPublicKeyRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/public-key");
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));
        var secretPublicKeyResult = githubClient.Execute<ActionsPublicKey>(secretPublicKeyRequest);

        if (string.IsNullOrWhiteSpace(secretPublicKeyResult.Data?.Key))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "we couldn't get secrets key. If this keeps happening contact 383@envoc.com");
        }

        using var stream = file.OpenReadStream();
        var secretValue = new byte[file.Length];
        var _ = await stream.ReadAsync(secretValue);
        var publicKey = Convert.FromBase64String(secretPublicKeyResult.Data.Key);
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretValue, publicKey);
        var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);

        var putSecretRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/{secret_name}", Method.Put);
        putSecretRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        putSecretRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));

        const string publishProfileKey = "AZURE_WEBAPP_PUBLISH_PROFILE";
        putSecretRequest.AddParameter(Parameter.CreateParameter("secret_name", publishProfileKey, ParameterType.UrlSegment));

        putSecretRequest.AddBody(new CreateRepositorySecret
        {
            EncryptedValue = encryptedValue,
            KeyId = secretPublicKeyResult.Data.KeyId
        });
        var saved = (await githubClient.ExecuteAsync(putSecretRequest)).IsSuccessful;
        if (!saved)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "Bad things occured");
        }

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"The secret as been set - you should be able to use {publishProfileKey} as a secret value in your actions");
    }
}

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
        var accessError = await userData.GetAccessError(repository);
        if (accessError != null)
        {
            return accessError;
        }

        var file = form.Files.FirstOrDefault();
        if (file == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing file");
        }

        var githubClient = FunctionHelper.GetGithubClient();
        var secretPublicKeyRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/public-key");
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));
        var secretPublicKeyResult = githubClient.Execute<ActionsPublicKey>(secretPublicKeyRequest);

        if (string.IsNullOrWhiteSpace(secretPublicKeyResult.Data?.Key))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "we couldn't get secrets key. If this keeps happening contact 383@envoc.com");
        }

        var publicKey = Convert.FromBase64String(secretPublicKeyResult.Data.Key);

        const string publishProfileKey = "AZURE_WEBAPP_PUBLISH_PROFILE";

        var encryptedValue = await GetEncryptedValue(file, publicKey);

        var publishError = await UpdateSecret(publishProfileKey, encryptedValue);
        if (publishError != null)
        {
            return publishError;
        }

        const string expoTokenKey = "EXPO_TOKEN";
        var expoError = await UpdateSecret(expoTokenKey, GetEncryptedValue(FunctionHelper.GetEnvironmentVariable("ExpoToken"), publicKey));
        if (expoError != null)
        {
            return expoError;
        }

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"The secret as been set - you should be able to use {publishProfileKey} and {expoTokenKey} as a secret values in your actions");

        async Task<ContentResult?> UpdateSecret(string key, string encryptedResult)
        {
            var putSecretRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/{secret_name}", Method.Put);
            putSecretRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
            putSecretRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));

            putSecretRequest.AddParameter(Parameter.CreateParameter("secret_name", key, ParameterType.UrlSegment));

            putSecretRequest.AddBody(new CreateRepositorySecret
            {
                EncryptedValue = encryptedResult,
                KeyId = secretPublicKeyResult.Data.KeyId
            });
            var saved = (await githubClient.ExecuteAsync(putSecretRequest)).IsSuccessful;
            if (!saved)
            {
                return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "Bad things occured");
            }

            return null;
        }
    }

    private static string GetEncryptedValue(string secretString, byte[] publicKey)
    {
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretString, publicKey);
        var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);
        return encryptedValue;
    }

    private static async Task<string> GetEncryptedValue(IFormFile secretFile, byte[] publicKey)
    {
        using var stream = secretFile.OpenReadStream();
        var secretValue = new byte[secretFile.Length];
        var _ = await stream.ReadAsync(secretValue);
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretValue, publicKey);
        var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);
        return encryptedValue;
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RestSharp;
using Selu383Bot.GithubWebhook.Features.Secrets;

namespace Selu383Bot.GithubWebhook.Helpers;

public static class GithubSecretHelpers
{
    public static async Task<bool> UpdateSecret(string repository, ActionsPublicKey actionsPublicKey, string secretName, string encryptedResult)
    {
        var githubClient = FunctionHelper.GetNewtonsoftGithubApiClient();
        var putSecretRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/{secret_name}", Method.Put);
        putSecretRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        putSecretRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));

        putSecretRequest.AddParameter(Parameter.CreateParameter("secret_name", secretName, ParameterType.UrlSegment));

        putSecretRequest.AddBody(new CreateRepositorySecret
        {
            EncryptedValue = encryptedResult,
            KeyId = actionsPublicKey.KeyId
        });
        return (await githubClient.ExecuteAsync(putSecretRequest)).IsSuccessful;
    }

    public static ActionsPublicKey GetPublicKey(string repository)
    {
        var githubClient = FunctionHelper.GetNewtonsoftGithubApiClient();
        var secretPublicKeyRequest = new RestRequest("/repos/{owner}/{repo}/actions/secrets/public-key");
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        secretPublicKeyRequest.AddParameter(Parameter.CreateParameter("repo", repository, ParameterType.UrlSegment));
        var secretPublicKeyResult = githubClient.Execute<ActionsPublicKey>(secretPublicKeyRequest);

        if (string.IsNullOrWhiteSpace(secretPublicKeyResult.Data?.Key))
        {
            return null;
        }

        var result = secretPublicKeyResult.Data;
        result.KeyBytes = Convert.FromBase64String(secretPublicKeyResult.Data.Key);
        return result;
    }

    public static string GetEncryptedValue(string secretString, ActionsPublicKey publicKey)
    {
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretString, publicKey.KeyBytes);
        var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);
        return encryptedValue;
    }

    public static async Task<string> GetEncryptedValue(IFormFile secretFile, ActionsPublicKey publicKey)
    {
        using var stream = secretFile.OpenReadStream();
        var secretValue = new byte[secretFile.Length];
        var _ = await stream.ReadAsync(secretValue);
        var sealedPublicKeyBox = Sodium.SealedPublicKeyBox.Create(secretValue, publicKey.KeyBytes);
        var encryptedValue = Convert.ToBase64String(sealedPublicKeyBox);
        return encryptedValue;
    }
}

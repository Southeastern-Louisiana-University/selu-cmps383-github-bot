using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using Selu383Bot.Features.Expo;
using Selu383Bot.Helpers;

namespace Selu383Bot.Functions;

public static class ExpoBuild
{
    [Function("expo-build")]
    public static async Task<IActionResult> ExpoBuildFinished(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expo-build/{repositoryName}")]
        HttpRequest httpRequest,
        string repositoryName,
        ILogger log)
    {
        var requestBody = await new StreamReader(httpRequest.Body).ReadToEndAsync();
        log.LogInformation("got body");
        log.LogInformation(requestBody);

        if (!IsAuthorized(httpRequest, requestBody))
        {
            log.LogError("unauthorized");
            return new UnauthorizedResult();
        }

        var build = JsonConvert.DeserializeObject<Build>(requestBody);

        if (string.IsNullOrWhiteSpace(build?.Artifacts?.BuildUrl))
        {
            log.LogError("BuildUrl is missing");
            return new BadRequestResult();
        }

        if (string.IsNullOrWhiteSpace(build.Metadata?.GitCommitHash))
        {
            log.LogError("GitCommitHash is missing");
            return new BadRequestResult();
        }

        var githubClient = new RestClient("https://api.github.com");
        githubClient.AddDefaultHeader("Authorization", $"token {FunctionHelper.GetEnvironmentVariable("GithubFineGrainAccessToken")}");

        var releaseCreateRequest = new RestRequest("/repos/{owner}/{repo}/releases", Method.Post);
        releaseCreateRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
        releaseCreateRequest.AddParameter(Parameter.CreateParameter("repo", repositoryName, ParameterType.UrlSegment));

        var timezone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var shortHash = build.Metadata.GitCommitHash[..7];
        var releaseId = $"v{now:yyyy.MM.dd}.{shortHash}";
        releaseCreateRequest.AddBody(new
        {
            tag_name = releaseId,
            target_commitish = build.Metadata.GitCommitHash,
            body = build.Artifacts.BuildUrl
        });

        var releaseCreateResult = await githubClient.ExecuteAsync(releaseCreateRequest);
        if (!releaseCreateResult.IsSuccessful || releaseCreateResult.Content == null)
        {
            log.LogError("Could not create release");
            log.LogError(JsonConvert.SerializeObject(releaseCreateResult, Formatting.Indented));

            return new StatusCodeResult(500);
        }

        // TODO: figure out why release assets are not working
        // see: https://docs.github.com/en/rest/releases/assets#upload-a-release-asset

        return new OkResult();
    }

    private static bool IsAuthorized(HttpRequest httpRequest, string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return false;
        }

        var webhookSecret = FunctionHelper.GetEnvironmentVariable("ExpoWebhookSecret");
        if (webhookSecret == "ignore")
        {
            return true;
        }

        httpRequest.Headers.TryGetValue("Expo-Signature", out var signatureWithPrefix);

        if (string.IsNullOrWhiteSpace(signatureWithPrefix))
        {
            return false;
        }

        var secret = Encoding.ASCII.GetBytes(webhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(requestBody);
        using var sha = new HMACSHA1(secret);
        var computedHash = sha.ComputeHash(payloadBytes);
        string signatureWithPrefixString = signatureWithPrefix;
        var signatureString = signatureWithPrefixString.Replace("sha1=", string.Empty);
        var signatureBytes = Convert.FromHexString(signatureString);

        return CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes);
    }
}

using System;
using System.IO;
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
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Serializers;
using Selu383Bot.GithubWebhook.Features.BranchProtections;
using Selu383Bot.GithubWebhook.Features.RateLimits;
using Selu383Bot.GithubWebhook.Features.Webhook;

namespace Selu383Bot.GithubWebhook;

public static class RepositoryHook
{
    // note: this is fixed so we don't oops elsewhere
    const string SeluOrganization = "Southeastern-Louisiana-University";

    [FunctionName("RepositoryHook")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
        HttpRequest httpRequest)
    {
        var sb = new StringBuilder();
        try
        {
            string requestBody = await new StreamReader(httpRequest.Body).ReadToEndAsync();
            if (!IsAuthorized(httpRequest, requestBody))
            {
                AppendLine("Auth failed");
                return Status(HttpStatusCode.Unauthorized);
            }

            using var githubClient = new RestClient("https://api.github.com")
                .UseSerializer(()=> new JsonNetSerializer());

            var data = GetEnvironmentVariable("GithubAuthToken");
            githubClient.AddDefaultHeader("Authorization", $"token {data}");

            var rageLimit = new RestRequest("/rate_limit");
            var rateLimitResult = await githubClient.ExecuteAsync<RateLimit>(rageLimit);
            if (rateLimitResult.StatusCode != HttpStatusCode.OK || rateLimitResult.Data == null)
            {
                AppendLine("Failed rate check");
                AppendJson(rateLimitResult);
                return Status(HttpStatusCode.InternalServerError);
            }

            AppendLine("Current rate limit:");
            AppendJson(rateLimitResult.Data);

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                AppendLine("No body");
                return Status(HttpStatusCode.OK);
            }

            var orderedElements = JsonConvert.DeserializeObject<JObject>(requestBody);

            if (!orderedElements.ContainsKey("action"))
            {
                AppendLine("No action found");
                return Status(HttpStatusCode.OK);
            }

            var result = new Event();

            foreach (var element in orderedElements)
            {
                if (element.Value == null)
                {
                    continue;
                }

                if (element.Key == "action" && element.Value.Type == JTokenType.String)
                {
                    result.Action = element.Value.Value<string>();
                }

                if (element.Key == "scope" && element.Value.Type == JTokenType.String)
                {
                    result.Scope = element.Value.Value<string>();
                }

                if (element.Value.Type == JTokenType.Object && result.TargetType == null)
                {
                    result.TargetType = element.Key;
                    result.Target = element.Value.Value<JObject>();
                }
            }

            if (result.Action == null || result.TargetType == null)
            {
                AppendLine("No action or target type found");
                return Status(HttpStatusCode.OK);
            }

            if (result.Action != "created" || result.TargetType != "repository")
            {
                AppendLine("not a created repository action - so we are skipping");
                return Status(HttpStatusCode.OK);
            }

            if (result.Action != "created" || result.TargetType != "repository")
            {
                AppendLine("not a created repository action - so we are skipping");
                return Status(HttpStatusCode.OK);
            }

            result.Payload = JsonConvert.DeserializeObject<EventPayload>(requestBody);
            if (result.Payload == null)
            {
                AppendLine("failed to get payload information");
                return Status(HttpStatusCode.InternalServerError);
            }

            var repository = result.Payload.Repository;
            if (repository == null || repository.Name == null)
            {
                AppendLine("failed to get repository information");
                return Status(HttpStatusCode.InternalServerError);
            }

            if (result.Payload.Organization?.Login != SeluOrganization)
            {
                AppendLine("we should only be processing selu organization things");
                return Status(HttpStatusCode.InternalServerError);
            }

            if (!repository.Name.Contains("cmps383"))
            {
                AppendLine("Looks like this isn't 383 - let's bounce");
                return Status(HttpStatusCode.OK);
            }

            var branchProtection = new RestRequest("/repos/{owner}/{repo}/branches/{branch}/protection", Method.Put);
            branchProtection.AddParameter(Parameter.CreateParameter("owner", SeluOrganization, ParameterType.UrlSegment));
            branchProtection.AddParameter(Parameter.CreateParameter("repo", repository.Name, ParameterType.UrlSegment));
            branchProtection.AddParameter(Parameter.CreateParameter("branch", "master", ParameterType.UrlSegment));
            branchProtection.AddBody(new BranchProtection
            {
                RequiredPullRequestReviews = new RequiredPullRequestReviews()
            });

            var branchProtectionResult = await githubClient.ExecuteAsync(branchProtection);
            if (branchProtectionResult.StatusCode != HttpStatusCode.OK)
            {
                AppendLine("Error applying branch protection");
                AppendJson(branchProtectionResult);
                return Status(HttpStatusCode.InternalServerError);
            }

            return Status(HttpStatusCode.OK);
        }
        catch (Exception e)
        {
            AppendLine("Error processing request");
            AppendLine(e.ToString());
            return Status(HttpStatusCode.InternalServerError);
        }

        IActionResult Status(HttpStatusCode code)
        {
            return ReturnResult(code, sb);
        }

        void AppendLine(string message)
        {
            sb.AppendLine(message);
        }

        void AppendJson<T>(T payload) where T:class
        {
            AppendLine(JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
    }

    private static bool IsAuthorized(HttpRequest httpRequest, string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return false;
        }

        var webhookSecret = GetEnvironmentVariable("WebhookSecret");

        httpRequest.Headers.TryGetValue("X-Hub-Signature-256", out var signatureWithPrefix);

        if (string.IsNullOrWhiteSpace(signatureWithPrefix))
        {
            return false;
        }

        var secret = Encoding.ASCII.GetBytes(webhookSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(requestBody);
        using var sha = new HMACSHA256(secret);
        var computedHash = sha.ComputeHash(payloadBytes);
        string signatureWithPrefixString = signatureWithPrefix;
        var signatureString = signatureWithPrefixString.Replace("sha256=", string.Empty);
        var signatureBytes = Convert.FromHexString(signatureString);

        return CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes);
    }

    private static IActionResult ReturnResult(HttpStatusCode code, StringBuilder sb)
    {
        return new ContentResult()
        {
            StatusCode = (int)code,
            Content = sb.ToString(),
            ContentType = "text/plain"
        };
    }

    private static string GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ??
               Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }
}

public class JsonNetSerializer : IRestSerializer, ISerializer, IDeserializer
{
    private static readonly string[] Types = {
        "application/json", "text/json", "text/x-json", "text/javascript", "*+json"
    };

    public string Serialize(Parameter parameter) => Serialize(parameter.Value);

    public ISerializer Serializer => this;
    public IDeserializer Deserializer => this;

    public string[] AcceptedContentTypes => Types;

    public SupportsContentType SupportsContentType { get; } = x => Types.Contains(x);

    public DataFormat DataFormat => DataFormat.Json;

    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public string ContentType { get; set; } = Types[0];

    public T Deserialize<T>(RestResponse response)
    {
        if (response.Content == null)
        {
            return default;
        }
        return JsonConvert.DeserializeObject<T>(response.Content);
    }
}

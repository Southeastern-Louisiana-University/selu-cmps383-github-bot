using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Selu383Bot.Features.BranchProtections;
using Selu383Bot.Features.CommitStatuses;
using Selu383Bot.Features.RateLimits;
using Selu383Bot.Features.Teams;
using Selu383Bot.Features.Webhook;
using Selu383Bot.Helpers;
using Team = Selu383Bot.Features.Teams.Team;

namespace Selu383Bot.Functions;

public static class RepositoryHook
{
    [Function("RepositoryHook")]
    public static async Task<ContentResult> RunAsync(
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

            var githubClient = new RestClient("https://api.github.com").UseSerializer(()=> new JsonNetSerializer());
            githubClient.AddDefaultHeader("Authorization", $"token {FunctionHelper.GetEnvironmentVariable("GithubFineGrainAccessToken")}");

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
            if (orderedElements == null)
            {
                AppendLine("No body");
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

            if (result.TargetType == null)
            {
                AppendLine("No target type found");
                return Status(HttpStatusCode.OK);
            }

            result.Payload = JsonConvert.DeserializeObject<EventPayload>(requestBody);
            if (result.Payload == null)
            {
                AppendLine("failed to get payload information");
                return Status(HttpStatusCode.InternalServerError);
            }

            if (result.Payload?.Repository?.Name == null)
            {
                AppendLine("failed to get repository information");
                return Status(HttpStatusCode.OK);
            }

            if (result.Payload.Organization?.Login != FunctionHelper.SeluOrganization)
            {
                AppendLine("we should only be processing selu organization things");
                return Status(HttpStatusCode.InternalServerError);
            }

            if (!result.Payload.Repository.Name.Contains("cmps383"))
            {
                AppendLine("Looks like this isn't 383 - let's bounce");
                return Status(HttpStatusCode.OK);
            }

            await FunctionHelper.WriteToStudentBlobAsync(result.Payload.Repository.Name, requestBody);

            Func<Task<ContentResult>> repoAction = result switch
            {
                { TargetType: "repository", Action: "created" } => async () =>
                {
                    var addTeamError = await AddAdminTeam();
                    if (addTeamError != null)
                    {
                        return addTeamError;
                    }

                    return await ApplyBranchProtection();
                },
                { TargetType: "check_suite", Action: "completed" } => SetCheckSuiteStatus,
                { TargetType: "team", Action: null } => RenameTeam,
                _ => null
            };

            if (repoAction == null)
            {
                AppendLine($"event not handled: {result.Action} {result.TargetType}");
                return Status(HttpStatusCode.OK);
            }

            AppendLine($"Performing process for: {result.Action} {result.TargetType}");

            return await repoAction();

            async Task<ContentResult> AddAdminTeam()
            {
                var repository = result.Payload.Repository;
                var teamPermissionRequest = new RestRequest("/orgs/{owner}/teams/{team_slug}/repos/{owner}/{repo}", Method.Put);
                teamPermissionRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
                teamPermissionRequest.AddParameter(Parameter.CreateParameter("repo", repository.Name, ParameterType.UrlSegment));
                teamPermissionRequest.AddParameter(Parameter.CreateParameter("team_slug", FunctionHelper.AdminTeamSlug, ParameterType.UrlSegment));
                teamPermissionRequest.AddBody(new TeamPermission
                {
                    Permission = "admin"
                });

                var teamPermissionResult = await githubClient.ExecuteAsync(teamPermissionRequest);
                if (!teamPermissionResult.IsSuccessful)
                {
                    AppendLine("Error applying admin team permission");
                    AppendJson(teamPermissionResult);
                    return Status(HttpStatusCode.InternalServerError);
                }

                return null;
            }

            async Task<ContentResult> ApplyBranchProtection()
            {
                var repository = result.Payload.Repository;
                var branchProtection = new RestRequest("/repos/{owner}/{repo}/branches/{branch}/protection", Method.Put);
                branchProtection.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
                branchProtection.AddParameter(Parameter.CreateParameter("repo", repository.Name, ParameterType.UrlSegment));
                branchProtection.AddParameter(Parameter.CreateParameter("branch", "master", ParameterType.UrlSegment));
                branchProtection.AddBody(new BranchProtection
                {
                    RequiredPullRequestReviews = new RequiredPullRequestReviews(),
                    RequiredStatusChecks = new RequiredStatusChecks
                    {
                        Strict = true,
                        Contexts = new List<string>
                        {
                            "Selu383Bot"
                        }
                    }
                });

                var branchProtectionResult = await githubClient.ExecuteAsync(branchProtection);
                if (!branchProtectionResult.IsSuccessful)
                {
                    AppendLine("Error applying branch protection");
                    AppendJson(branchProtectionResult);
                    return Status(HttpStatusCode.InternalServerError);
                }

                var mergeProtection = new RestRequest("/repos/{owner}/{repo}", Method.Patch);
                mergeProtection.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
                mergeProtection.AddParameter(Parameter.CreateParameter("repo", repository.Name, ParameterType.UrlSegment));
                mergeProtection.AddBody(new
                {
                    //https://docs.github.com/en/rest/repos/repos?apiVersion=2022-11-28#update-a-repository
                    allow_squash_merge = false,
                    allow_rebase_merge = false,
                });

                var mergeProtectionResult = await githubClient.ExecuteAsync(mergeProtection);
                if (!mergeProtectionResult.IsSuccessful)
                {
                    AppendLine("Error applying merge protection");
                    AppendJson(mergeProtectionResult);
                    return Status(HttpStatusCode.InternalServerError);
                }

                return Status(HttpStatusCode.OK);
            }

            async Task<ContentResult> SetCheckSuiteStatus()
            {
                var repository = result.Payload.Repository;
                var checkSuite = result.Payload.CheckSuite;
                if (checkSuite?.After == null)
                {
                    AppendLine("Invalid check suite");
                    return Status(HttpStatusCode.InternalServerError);
                }
                var commitStatus = new RestRequest("/repos/{owner}/{repo}/statuses/{sha}", Method.Post);
                commitStatus.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
                commitStatus.AddParameter(Parameter.CreateParameter("repo", repository.Name, ParameterType.UrlSegment));
                commitStatus.AddParameter(Parameter.CreateParameter("sha", checkSuite.After, ParameterType.UrlSegment));

                commitStatus.AddBody(new CommitStatus
                {
                    Description = "Check Suite: " + checkSuite.Conclusion,
                    State = checkSuite.Conclusion
                });
                var commitStatusResult = await githubClient.ExecuteAsync(commitStatus);
                if (!commitStatusResult.IsSuccessful)
                {
                    AppendLine("Error setting commit status");
                    AppendJson(commitStatus);
                    return Status(HttpStatusCode.InternalServerError);
                }

                return Status(HttpStatusCode.OK);
            }

            async Task<ContentResult> RenameTeam()
            {
                var repository = result.Payload.Repository;
                var team = result.Payload.Team;
                if (team.Slug == FunctionHelper.AdminTeamSlug)
                {
                    return Status(HttpStatusCode.OK);
                }

                var teamName = new RestRequest("/orgs/{org}/teams/{team_slug}", Method.Patch);
                teamName.AddParameter(Parameter.CreateParameter("org", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
                teamName.AddParameter(Parameter.CreateParameter("team_slug", team.Slug, ParameterType.UrlSegment));
                teamName.AddBody(new Team
                {
                    Name = repository.Name,
                    Description = team.Description,
                    Privacy = team.Privacy
                });
                var teamNameResult = await githubClient.ExecuteAsync(teamName);
                if (!teamNameResult.IsSuccessful)
                {
                    AppendLine("Error setting team name");
                    AppendJson(teamName);
                    return Status(HttpStatusCode.InternalServerError);
                }

                return Status(HttpStatusCode.OK);
            }
        }
        catch (Exception e)
        {
            AppendLine("Error processing request");
            AppendLine(e.ToString());
            return Status(HttpStatusCode.InternalServerError);
        }

        ContentResult Status(HttpStatusCode code)
        {
            return FunctionHelper.ReturnResult(code, sb);
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

        var webhookSecret = FunctionHelper.GetEnvironmentVariable("WebhookSecret");
        if (webhookSecret == "ignore")
        {
            return true;
        }

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
}

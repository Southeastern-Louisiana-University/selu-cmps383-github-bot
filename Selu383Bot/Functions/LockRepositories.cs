using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RestSharp;
using Selu383Bot.Features.Teams;
using Selu383Bot.Helpers;

namespace Selu383Bot.Functions;

public static class LockRepositories
{
    // TODO: setting?
    private const string TeamSlugContainsString = "cmps383-2026-sp";

    [Function("LockRepositories")]
    public static async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
    {
        var now = DateTimeOffset.UtcNow;
        Console.WriteLine($"LockRepositories executed at: {now}");

        var lockout = FunctionHelper.GetEnvironmentVariable("LockoutDate");
        if (string.IsNullOrWhiteSpace(lockout) || !DateTimeOffset.TryParse(lockout, out var parsed))
        {
            Console.WriteLine("Invalid lockout: " + (lockout ?? "NULL"));
            return;
        }

        if ((parsed - now).Duration() >= TimeSpan.FromMinutes(30))
        {
            Console.WriteLine("Lock out doesn't meet time window: " + lockout);
            return;
        }

        var githubClient = new RestClient("https://api.github.com");
        githubClient.AddDefaultHeader("Authorization", $"token {FunctionHelper.GetEnvironmentVariable("GithubFineGrainAccessToken")}");

        var relevantTeams = new List<TeamResult>();

        var page = 1;
        while (true)
        {
            var teamRequest = new RestRequest("/orgs/{owner}/teams");
            teamRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
            teamRequest.AddParameter(Parameter.CreateParameter("page", page, ParameterType.QueryString));
            teamRequest.AddParameter(Parameter.CreateParameter("per_page", 100, ParameterType.QueryString));
            var teamResult = await githubClient.ExecuteAsync<TeamResult[]>(teamRequest);
            if (!teamResult.IsSuccessful || teamResult.Data == null || !teamResult.Data.Any())
            {
                break;
            }
            page++;
            relevantTeams.AddRange(teamResult.Data.Where(x => x.Slug.Contains(TeamSlugContainsString)));
        }

        foreach (var relevantTeam in relevantTeams)
        {
            var teamPermissionRequest = new RestRequest("/orgs/{owner}/teams/{team_slug}/repos/{owner}/{repo}", Method.Put);
            teamPermissionRequest.AddParameter(Parameter.CreateParameter("owner", FunctionHelper.SeluOrganization, ParameterType.UrlSegment));
            // we can cheat - we renamed the team to match the repository values identically
            teamPermissionRequest.AddParameter(Parameter.CreateParameter("repo", relevantTeam.Slug, ParameterType.UrlSegment));
            teamPermissionRequest.AddParameter(Parameter.CreateParameter("team_slug", relevantTeam.Slug, ParameterType.UrlSegment));
            teamPermissionRequest.AddBody(new
            {
                permission = "pull"
            });

            Console.WriteLine($"Locking out {relevantTeam.Slug}");

            await githubClient.ExecuteAsync(teamPermissionRequest);
        }
    }
}

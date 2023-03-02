using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;

namespace Selu383Bot.GithubWebhook.Extensions;

public static class EncryptedUserDtoExtensions
{
    public static async Task<IActionResult> GetAccessError(this EncryptedUserDto userData,string repository)
    {
        if (string.IsNullOrEmpty(repository))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing repository");
        }

        if (!repository.Contains("cmps383"))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "You should only be using this for 383 :(");
        }

        var githubClient = FunctionHelper.GetGithubClient();
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

        return null;
    }
}

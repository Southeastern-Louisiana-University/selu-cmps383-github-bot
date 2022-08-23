using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Graph.RBAC.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Graph;
using Newtonsoft.Json;
using RestSharp;
using Selu383Bot.GithubWebhook.Extensions;
using Selu383Bot.GithubWebhook.Features.Azure;
using Selu383Bot.GithubWebhook.Features.Users;
using Selu383Bot.GithubWebhook.Helpers;
using Selu383Bot.GithubWebhook.Properties;
using RestClient = RestSharp.RestClient;
using FluentAzure = Microsoft.Azure.Management.Fluent.Azure;

namespace Selu383Bot.GithubWebhook.Functions;

public static class SetAzureEmail
{
    [FunctionName("set-email-get")]
    public static Task<IActionResult> SetEmailGet([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "set-email")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "email");
            return Task.FromResult<IActionResult>(new RedirectResult("/api/start-github-login", false));
        }

        return Task.FromResult<IActionResult>((new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = Resources.SetEmailAddress
        }));
    }

    [FunctionName("set-email-post")]
    public static async Task<IActionResult> SetEmailPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "set-email")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "email");
            return new RedirectResult("/api/start-github-login", false);
        }

        var form = await req.ReadFormAsync();
        var email = form["email"].ToString().Trim();
        if (string.IsNullOrEmpty(email))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing email");
        }

        var repository = form["repository"].ToString().Trim();
        if (string.IsNullOrEmpty(repository))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "Missing repository");
        }

        if (!repository.Contains("cmps383"))
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.BadRequest, "You should only be using this for 383 :(");
        }

        var githubClient = new RestClient("https://api.github.com").UseSerializer(()=> new JsonNetSerializer());
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

        var nameBase = repository.ToLowerInvariant();

        var ownerServicePrincipal = JsonConvert.DeserializeObject<ServicePrincipalData>(FunctionHelper.GetEnvironmentVariable("AzureServicePrincipalData")) ?? throw new Exception("missing AzureServicePrincipalData");
        var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(ownerServicePrincipal.AppId, ownerServicePrincipal.Password, ownerServicePrincipal.Tenant, AzureEnvironment.AzureGlobalCloud);

        var authenticated = FluentAzure.Authenticate(credentials);
        var azure = authenticated.WithSubscription(ownerServicePrincipal.SubscriptionId);

        var credential = new ClientSecretCredential(ownerServicePrincipal.Tenant, ownerServicePrincipal.AppId, ownerServicePrincipal.Password);
        var graphServiceClient = new GraphServiceClient(credential);

        return await ProvisionGroup(graphServiceClient, azure, nameBase, email, userData.Username);
    }

    private static async Task<IActionResult> ProvisionGroup(
        GraphServiceClient graphServiceClient,
        IAzure azure,
        string nameBase,
        string email,
        string githubUsername)
    {
        var resourceGroup = await GetOrCreateResourceGroup(azure, nameBase + "-resource-group");

        var adUser = await GetOrCreateAdUser(graphServiceClient, email, githubUsername);
        if (adUser == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "We had trouble creating your user. Try again. Email 383@envoc.com if it continues to fail");
        }
        var adGroup = await GetOrCreateGroup(graphServiceClient, nameBase);

        try
        {
            await graphServiceClient.Groups[adGroup.Id].Members.References
                .Request()
                .AddAsync(adUser);
        }
        catch (ServiceException e) when (e.Message.Contains("One or more added object references already exist"))
        {
            // try and fail is fastest
        }

        var roles = new[] { BuiltInRole.WebsiteContributor, BuiltInRole.SqlSecurityManager, BuiltInRole.SqlServerContributor, BuiltInRole.SqlDbContributor };
        foreach (var role in roles)
        {
            await AddGroupRole(azure, adGroup, resourceGroup, role);
        }

        var allGroups = await azure.ResourceGroups.ListAsync();
        var sharedResourceGroup = allGroups
            .Where(x => x.Name.EndsWith("-shared"))
            .SingleOrDefault(x => x.Name.Split("-").Length == 2);
        if (sharedResourceGroup == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "Envoc needs to create the shared resource stuff");
        }

        await AddGroupRole(azure, adGroup, sharedResourceGroup, BuiltInRole.WebPlanContributor);

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"Hi {adUser.DisplayName} check {adUser.Mail} for an azure invite. It will be titled 'SELU 383 Envoc invited you to access applications within their organization' ");
    }

    private static async Task AddGroupRole(
        IAzure azure,
        Group adGroup,
        IResourceGroup resourceGroup,
        BuiltInRole role)
    {
        while (true)
        {
            try
            {
                await azure.AccessManagement.RoleAssignments.Define(SdkContext.RandomGuid())
                    .ForObjectId(adGroup.Id)
                    .WithBuiltInRole(role)
                    .WithResourceScope(resourceGroup)
                    .CreateAsync();

                return;
            }
            catch (Exception e) when(e.Message == "The role assignment already exists.")
            {
                return;
            }
        }
    }

    private static async Task<User> GetOrCreateAdUser(GraphServiceClient graphServiceClient, string email, string githubUsername)
    {
        // appends github to avoid shenanigans
        var displayName = githubUsername + "_github";
        var emailExists = await graphServiceClient.Users.Request()
            .Filter($"mail eq '{email}'")
            .GetResponseAsync();
        var emailExistsResult = await emailExists.GetResponseObjectAsync();
        if (emailExistsResult.Value.Any())
        {
            // email exists
            return emailExistsResult.Value.First();
        }

        var displayNameExists = await graphServiceClient.Users.Request()
            .Filter($"displayName eq '{displayName}'")
            .GetResponseAsync();
        var displayNameExistsResults = await displayNameExists.GetResponseObjectAsync();
        if (displayNameExistsResults.Value.Any())
        {
            // display name already present
            return displayNameExistsResults.Value.First();
        }

        var dic = new Dictionary<string, object> { { "@odata.type", "microsoft.graph.invitedUserMessageInfo" } };
        var invitation = new Invitation
        {
            InvitedUserEmailAddress = email,
            InvitedUserMessageInfo = new InvitedUserMessageInfo { AdditionalData = dic },
            InvitedUserDisplayName = displayName,
            SendInvitationMessage = true,
            InviteRedirectUrl = "https://portal.azure.com/",
        };

        await graphServiceClient.Invitations.Request().AddAsync(invitation);

        for (var i = 0; i < 60; i++)
        {
            try
            {
                await Task.Delay(1000);
                var created = await graphServiceClient.Users.Request()
                    .Filter($"mail eq '{email}'")
                    .GetResponseAsync();
                var createdResult = await created.GetResponseObjectAsync();
                return createdResult.Value.Single();
            }
            catch (Exception e) when(e.Message.Contains("Sequence contains no elements"))
            {
            }
        }

        return null;
    }

    private static async Task<Group> GetOrCreateGroup(GraphServiceClient graphserviceClient, string nameBase)
    {
        var getExistingRequest = await graphserviceClient.Groups.Request()
            .Filter($"displayName eq '{nameBase}'")
            .GetResponseAsync();
        var getExistingResponse = await getExistingRequest.GetResponseObjectAsync();
        var existingGroup = getExistingResponse.Value.SingleOrDefault();

        if (existingGroup != null)
        {
            return existingGroup;
        }

        var groupRequest = await graphserviceClient.Groups
            .Request()
            .AddResponseAsync(new Group
            {
                MailNickname = nameBase,
                DisplayName = nameBase,
                MailEnabled = false,
                SecurityEnabled = true,
                AdditionalData = new Dictionary<string, object>()
            });
        return await groupRequest.GetResponseObjectAsync();
    }

    private static async Task<IResourceGroup> GetOrCreateResourceGroup(IAzure azure, string groupName)
    {
        var existing = await GetResourceGroup(azure, groupName);
        if (existing != null)
        {
            return existing;
        }

        var result = await azure.ResourceGroups.Define(groupName)
            .WithRegion(Region.USSouthCentral)
            .CreateAsync();

        return result;
    }

    private static async Task<IResourceGroup> GetResourceGroup(IAzure azure, string groupName)
    {
        try
        {
            return await azure.ResourceGroups.GetByNameAsync(groupName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

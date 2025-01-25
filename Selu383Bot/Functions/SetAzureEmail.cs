

using System.Net;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Graph.RBAC.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Graph;
using Newtonsoft.Json;
using Selu383Bot.Extensions;
using Selu383Bot.Features.Azure;
using Selu383Bot.Helpers;
using Selu383Bot.Properties;
using FluentAzure = Microsoft.Azure.Management.Fluent.Azure;

namespace Selu383Bot.Functions;

public static class SetAzureEmail
{
    [Function("set-email-get")]
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

    [Function("set-email-post")]
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
        var accessError = await userData.GetAccessError(repository);
        if (accessError != null)
        {
            return accessError;
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

        var createResult = await GetOrCreateAdUser(graphServiceClient, email, githubUsername);
        if (createResult.adUser == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "We had trouble creating your user. Try again. Email 383@envoc.com if it continues to fail");
        }
        var adGroup = await GetOrCreateGroup(graphServiceClient, nameBase);

        try
        {
            await graphServiceClient.Groups[adGroup.Id].Members.References
                .Request()
                .AddAsync(createResult.adUser);
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

        if (createResult.adInvite != null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"Hi {createResult.adUser.DisplayName} check {createResult.adUser.Mail} for an azure invite. It will be titled 'SELU 383 Envoc invited you to access applications within their organization'. SAVE THIS: If you don't get the email, your link is: {createResult.adInvite.InviteRedeemUrl}");
        }

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"Hi {createResult.adUser.DisplayName} / {createResult.adUser.Mail} - It looks like you were already setup. The rest of your azure resources have been setup. If your email looks wrong then contact 383@envoc.com");
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

    private static async Task<(User adUser, Invitation adInvite)> GetOrCreateAdUser(GraphServiceClient graphServiceClient, string email, string githubUsername)
    {

        // TODO: maybe check for a sent but unredeemed invite?

        // appends github to avoid shenanigans
        var displayName = githubUsername + "_github";
        var emailExists = await graphServiceClient.Users.Request()
            .Filter($"mail eq '{email}'")
            .GetResponseAsync();
        var emailExistsResult = await emailExists.GetResponseObjectAsync();
        if (emailExistsResult.Value.Any())
        {
            // email exists
            return new (emailExistsResult.Value.First(), null);
        }

        var displayNameExists = await graphServiceClient.Users.Request()
            .Filter($"displayName eq '{displayName}'")
            .GetResponseAsync();
        var displayNameExistsResults = await displayNameExists.GetResponseObjectAsync();
        if (displayNameExistsResults.Value.Any())
        {
            // display name already present
            return new (displayNameExistsResults.Value.First(), null);
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

        var inviteResult = await graphServiceClient.Invitations.Request().AddAsync(invitation);

        for (var i = 0; i < 60; i++)
        {
            try
            {
                await Task.Delay(1000);
                var created = await graphServiceClient.Users.Request()
                    .Filter($"mail eq '{email}'")
                    .GetResponseAsync();
                var createdResult = await created.GetResponseObjectAsync();
                return new (createdResult.Value.Single(), inviteResult);
            }
            catch (Exception e) when(e.Message.Contains("Sequence contains no elements"))
            {
            }
        }

        return new (null, null);
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

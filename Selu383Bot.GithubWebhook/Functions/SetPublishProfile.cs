using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Selu383Bot.GithubWebhook.Extensions;
using Selu383Bot.GithubWebhook.Helpers;
using Selu383Bot.GithubWebhook.Properties;

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


        var publicKey = GithubSecretHelpers.GetPublicKey(repository);
        if (publicKey == null)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "we couldn't get secrets key. If this keeps happening contact 383@envoc.com");
        }

        const string publishProfileKey = "AZURE_WEBAPP_PUBLISH_PROFILE";

        var encryptedValue = await GithubSecretHelpers.GetEncryptedValue(file, publicKey);

        var saved = await GithubSecretHelpers.UpdateSecret(repository, publicKey, publishProfileKey, encryptedValue);
        if (!saved)
        {
            return FunctionHelper.ReturnResult(HttpStatusCode.InternalServerError, "Bad things occured");
        }

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, $"The secret as been set - you should be able to use {publishProfileKey} as a secret values in your actions");
    }
}

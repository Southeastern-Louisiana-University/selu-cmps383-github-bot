using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Selu383Bot.Extensions;
using Selu383Bot.Helpers;
using Selu383Bot.Properties;

namespace Selu383Bot.Functions;

public static class SetPublishProfile
{
    [Function("set-secret-get")]
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

    [Function("set-secret-post")]
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

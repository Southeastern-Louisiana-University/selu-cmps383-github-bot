using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Selu383Bot.Extensions;
using Selu383Bot.Features.StudentHooks;
using Selu383Bot.Helpers;
using Selu383Bot.Properties;
using FluentAzure = Microsoft.Azure.Management.Fluent.Azure;

namespace Selu383Bot.Functions;

public static class SetWebook
{
    [Function("set-webhook-get")]
    public static Task<IActionResult> SetEmailGet([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "set-webhook")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "webhook");
            return Task.FromResult<IActionResult>(new RedirectResult("/api/start-github-login", false));
        }

        return Task.FromResult<IActionResult>((new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = (int?) HttpStatusCode.OK,
            Content = Resources.SetWebHook
        }));
    }

    [Function("set-webhook-post")]
    public static async Task<IActionResult> SetEmailPost([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "set-webhook")] HttpRequest req)
    {
        var userData = req.GetAuthInfo();
        if (userData == null)
        {
            req.SetCookie("path", "webhook");
            return new RedirectResult("/api/start-github-login", false);
        }

        var form = await req.ReadFormAsync();
        var webhookUrl = form["webhookUrl"].ToString().Trim();
        var repository = form["repository"].ToString().Trim();
        var accessError = await userData.GetAccessError(repository);
        if (accessError != null)
        {
            return accessError;
        }

        var webhooks = await FunctionHelper.GetStudentWebhooksTable();
        var entity = new WebhookTableEntity(repository.ToLower(), "0")
        {
            Url = webhookUrl
        };

        await webhooks.ExecuteAsync(TableOperation.InsertOrReplace(entity));

        await FunctionHelper.WriteToStudentBlobAsync(repository, JsonConvert.SerializeObject(new { test = true, some = "value" }));

        return FunctionHelper.ReturnResult(HttpStatusCode.OK, "done!");
    }
}

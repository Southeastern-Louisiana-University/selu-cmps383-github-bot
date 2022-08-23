using System;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Selu383Bot.GithubWebhook.Features.Users;
using Sodium;

namespace Selu383Bot.GithubWebhook.Helpers;

public static class FunctionHelper
{
    // note: this is fixed so we don't oops elsewhere
    public const string SeluOrganization = "Southeastern-Louisiana-University";

    public static ContentResult ReturnResult(HttpStatusCode code, string text)
    {
        return new ContentResult()
        {
            StatusCode = (int)code,
            Content = text,
            ContentType = "text/plain"
        };
    }

    public static ContentResult ReturnResult(HttpStatusCode code, StringBuilder sb)
    {
        return ReturnResult(code, sb.ToString());
    }

    public static string GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ??
               Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }
}

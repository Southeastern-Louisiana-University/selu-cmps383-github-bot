using System;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Selu383Bot.GithubWebhook.Helpers;

public static class FunctionHelper
{
    public static ContentResult ReturnResult(HttpStatusCode code, StringBuilder sb)
    {
        return new ContentResult()
        {
            StatusCode = (int)code,
            Content = sb.ToString(),
            ContentType = "text/plain"
        };
    }

    public static string GetEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process) ??
               Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }
}
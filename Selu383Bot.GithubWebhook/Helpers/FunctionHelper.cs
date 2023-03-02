using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using RestSharp;
using Selu383Bot.GithubWebhook.Features.StudentHooks;

namespace Selu383Bot.GithubWebhook.Helpers;

public static class FunctionHelper
{
    // note: this is fixed so we don't oops elsewhere
    public const string SeluOrganization = "Southeastern-Louisiana-University";
    public const string AdminTeamSlug = "383-admins";
    public const string StudentHookBlobContainerName = "studenthookcalls";

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

    public static RestClient GetNewtonsoftGithubApiClient()
    {
        return new RestClient("https://api.github.com").UseSerializer(() => new JsonNetSerializer());
    }

    public static CloudTableClient GetCloudTableClient()
    {
        var storageAccount = CloudStorageAccount.Parse(GetEnvironmentVariable("AzureWebJobsStorage"));
        var tableClient = storageAccount.CreateCloudTableClient();
        return tableClient;
    }

    public static async Task<CloudTable> GetStudentWebhooksTable()
    {
        var tableClient = GetCloudTableClient();
        var webhooks = tableClient.GetTableReference("webhooks");
        await webhooks.CreateIfNotExistsAsync();
        return webhooks;
    }

    public static async Task<CloudBlockBlob> GetToStudentBlobAsync(string name)
    {
        var storageAccount = CloudStorageAccount.Parse(GetEnvironmentVariable("AzureWebJobsStorage"));
        var blobClient = storageAccount.CreateCloudBlobClient();
        var hookBlobContainer = blobClient.GetContainerReference(StudentHookBlobContainerName);
        await hookBlobContainer.CreateIfNotExistsAsync();

        var reference = hookBlobContainer.GetBlockBlobReference(name);
        await reference.FetchAttributesAsync();

        return reference;
    }
    public static async Task WriteToStudentBlobAsync(string repository, string data)
    {
        var storageAccount = CloudStorageAccount.Parse(GetEnvironmentVariable("AzureWebJobsStorage"));
        var blobClient = storageAccount.CreateCloudBlobClient();
        var hookBlobContainer = blobClient.GetContainerReference(StudentHookBlobContainerName);
        await hookBlobContainer.CreateIfNotExistsAsync();

        var blobBlob = hookBlobContainer.GetBlockBlobReference($"{repository}_{Guid.NewGuid()}.json");
        blobBlob.Properties.ContentType = "application/json";
        blobBlob.Metadata["repo"] = repository;

        await blobBlob.UploadTextAsync(data);
    }

    public static async Task<string> GetHostForStudentHookBlobAsync(CloudBlockBlob handle)
    {
        var table = await GetStudentWebhooksTable();
        var row = await table.ExecuteAsync(TableOperation.Retrieve<WebhookTableEntity>(handle.Metadata["repo"].ToLower(), "0"));
        var data = row.Result as WebhookTableEntity;
        return data?.Url;
    }
}

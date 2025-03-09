using System;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using RestSharp;
using Selu383Bot.Features.StudentHooks;

namespace Selu383Bot.Helpers;

public static class FunctionHelper
{
    // note: this is fixed so we don't oops elsewhere
    public const string SeluOrganization = "Southeastern-Louisiana-University";
    public const string AdminTeamSlug = "383-admins";
    public const string StudentHookBlobContainerName = "studenthookcalls";
    public const string QueueName = "webhooktosend";

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
        var accessToken = GetEnvironmentVariable("GithubFineGrainAccessToken");
        var githubClient = new RestClient("https://api.github.com").UseSerializer(() => new JsonNetSerializer());
        githubClient.AddDefaultHeader("Authorization", $"token {accessToken}");
        return githubClient;
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
        return reference;
    }

    public static async Task WriteToStudentBlobAsync(string repository, string data)
    {
        var storageAccount = CloudStorageAccount.Parse(GetEnvironmentVariable("AzureWebJobsStorage"));
        var blobClient = storageAccount.CreateCloudBlobClient();
        var hookBlobContainer = blobClient.GetContainerReference(StudentHookBlobContainerName);
        await hookBlobContainer.CreateIfNotExistsAsync();

        var name = $"{repository}_{Guid.NewGuid()}.json";
        var blobBlob = hookBlobContainer.GetBlockBlobReference(name);
        blobBlob.Properties.ContentType = "application/json";
        blobBlob.Metadata["repo"] = repository;

        await blobBlob.UploadTextAsync(data);

        var queueClient = storageAccount.CreateCloudQueueClient();
        var queue = queueClient.GetQueueReference(QueueName);
        await queue.CreateIfNotExistsAsync();

        await queue.AddMessageAsync(new CloudQueueMessage(name));
    }

    public static async Task<string?> GetHostForStudentHookBlobAsync(CloudBlockBlob handle)
    {
        var repo = handle.Name.Split("_").FirstOrDefault()?.ToLower();
        if (string.IsNullOrWhiteSpace(repo))
        {
            return null;
        }
        var table = await GetStudentWebhooksTable();
        var row = await table.ExecuteAsync(TableOperation.Retrieve<WebhookTableEntity>(repo, "0"));
        var data = row.Result as WebhookTableEntity;
        return data?.Url;
    }
}

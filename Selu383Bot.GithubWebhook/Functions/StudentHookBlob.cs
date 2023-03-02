using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Selu383Bot.GithubWebhook.Helpers;

namespace Selu383Bot.GithubWebhook.Functions;

public static class StudentHookBlob
{
    [FunctionName("StudentBobStorage")]
    public static async Task RunAsync([BlobTrigger(FunctionHelper.StudentHookBlobContainerName + "/{name}")]Stream myBlob, string name, ILogger log)
    {
        log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

        var handle = await FunctionHelper.GetToStudentBlobAsync(name);
        handle.Metadata.TryGetValue("attempts", out var attemptTextValue);
        var attemptCount = int.Parse(attemptTextValue ?? "0");
        if (attemptCount > 3)
        {
            await handle.DeleteAsync();
            return;
        }
        try
        {
            var url = await FunctionHelper.GetHostForStudentHookBlobAsync(handle);
            var httpClient = HttpClientFactory.Create();
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StreamContent(myBlob)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(handle.Properties.ContentType);
            await httpClient.SendAsync(request);
        }
        catch
        {
            handle.Metadata["attempts"] = (attemptCount + 1).ToString();
            await handle.SetPropertiesAsync();
            throw;
        }

        await handle.DeleteAsync();
    }
}

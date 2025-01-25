using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Selu383Bot.Helpers;

namespace Selu383Bot.Functions;

public static class StudentHookBlob
{
    [Function("StudentBobStorage")]
    public static async Task RunAsync([BlobTrigger(FunctionHelper.StudentHookBlobContainerName + "/{name}")]Stream myBlob, string name, ILogger log, IHttpClientFactory clientFactory)
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
            if (string.IsNullOrWhiteSpace(url))
            {
                await handle.DeleteAsync();
                return;
            }
            // TODO: client factory
            var httpClient = clientFactory.CreateClient();
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

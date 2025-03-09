using System.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Selu383Bot.Helpers;

namespace Selu383Bot.Functions;

public static class StudentHookBlob
{
    [Function("StudentBobStorage")]
    public static async Task RunAsync([QueueTrigger(FunctionHelper.QueueName)]string name, FunctionContext context)
    {
        var logger = context.GetLogger(nameof(StudentHookBlob));
        CloudBlockBlob handle;
        try
        {
            handle = await FunctionHelper.GetToStudentBlobAsync(name);
        }
        catch (Exception e)
        {
            logger.LogError("failed to send {name}", name);
            logger.LogError(e.ToString());
            return;
        }

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
            var clientFactory = context.InstanceServices.GetRequiredService<IHttpClientFactory>();
            var httpClient = clientFactory.CreateClient();

            var memoryStream = new MemoryStream();
            await handle.DownloadToStreamAsync(memoryStream);
            memoryStream.Position = 0;
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StreamContent(memoryStream)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(handle.Properties.ContentType);
            var result = await httpClient.SendAsync(request);
            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"failed to send {name} to {url}");
            }
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

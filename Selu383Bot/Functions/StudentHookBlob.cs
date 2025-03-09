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
        long attemptCount;
        var logger = context.GetLogger(nameof(StudentHookBlob));
        CloudBlockBlob handle;
        try
        {
            attemptCount = Convert.ToInt64(context.BindingContext.BindingData["DequeueCount"]);
            handle = await FunctionHelper.GetToStudentBlobAsync(name);
        }
        catch (Exception e)
        {
            logger.LogError("failed to send {name}", name);
            logger.LogError(e.ToString());
            return;
        }

        if (attemptCount > 1)
        {
            await handle.DeleteAsync();
            return;
        }

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
        await httpClient.SendAsync(request);

        await handle.DeleteAsync();
    }
}

using Microsoft.WindowsAzure.Storage.Table;

namespace Selu383Bot.GithubWebhook.Features.StudentHooks;

public class WebhookTableEntity : TableEntity
{
    public WebhookTableEntity()
    {
    }

    public WebhookTableEntity(string partitionKey, string rowKey) : base(partitionKey, rowKey)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
    }

    public string Url { get; set; }
}
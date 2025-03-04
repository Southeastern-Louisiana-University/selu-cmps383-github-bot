using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Selu383Bot.Features.Azure;
using Selu383Bot.Helpers;

namespace Selu383Bot.Functions;

public static partial class DeleteExpensiveDatabases
{
    [GeneratedRegex("GP_S_Gen.*_1")]
    private static partial Regex GeneralPurposeServerless();

    [Function("DeleteExpensiveDatabases")]
    public static async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
    {
        var ownerServicePrincipal = JsonConvert.DeserializeObject<ServicePrincipalData>(FunctionHelper.GetEnvironmentVariable("AzureServicePrincipalData")) ?? throw new Exception("missing AzureServicePrincipalData");
        var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(ownerServicePrincipal.AppId, ownerServicePrincipal.Password, ownerServicePrincipal.Tenant, AzureEnvironment.AzureGlobalCloud);

        var authenticated = Microsoft.Azure.Management.Fluent.Azure.Authenticate(credentials);
        var azure = authenticated.WithSubscription(ownerServicePrincipal.SubscriptionId);

        var servers = new List<(string id, string slo)>();

        var resourceGroups = await azure.ResourceGroups.ListAsync();
        while (resourceGroups != null && resourceGroups.Any())
        {
            var studentGroups = resourceGroups.Where(x => x.Id.Contains("/cmps383")).ToArray();
            foreach (var resourceGroup in studentGroups)
            {
                var sqlServers = azure.SqlServers.ListByResourceGroup(resourceGroup.Name);
                foreach (var server in sqlServers)
                {
                    var databases = await server.Databases.ListAsync();
                    foreach (var database in databases)
                    {
                        if(database.Name == "master")
                        {
                            continue;
                        }
                        servers.Add(new (server.Id, database.ServiceLevelObjective.ToString()));
                    }
                }
            }

            resourceGroups = await resourceGroups.GetNextPageAsync();
        }

        var gp = GeneralPurposeServerless();
        var toDelete = servers
            .Where(s => s.slo != "Basic" && !gp.IsMatch(s.slo))
            .Select(s => s.id)
            .ToArray();

        if (toDelete.Any())
        {
            await azure.SqlServers.DeleteByIdsAsync(toDelete);
        }
    }
}

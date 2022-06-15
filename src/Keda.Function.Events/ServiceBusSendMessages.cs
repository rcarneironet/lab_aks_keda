using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Keda.Function.SendEvents
{
    public static class ServiceBusSendMessages
    {

        private const string queueName = "kedafila";
        private const int messageNumber = 5000;

        [FunctionName("ServiceBusSendMessages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var connectionString = "<service_bus_connection_string>";

            await using (var client = new ServiceBusClient(connectionString))
            {
                var sender = client.CreateSender(queueName);

                for (int i = 0; i < messageNumber; i++)
                {
                    var messageBus = new ServiceBusMessage($"Message #{i}");

                    await sender
                            .SendMessageAsync(messageBus)
                            .ConfigureAwait(false);

                    log.LogInformation($"Message {i} sent to queue.");
                }
            }
            return new OkObjectResult("This HTTP triggered function executed successfully!");
        }
    }
}

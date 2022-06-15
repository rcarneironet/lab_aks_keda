using Azure.Messaging.ServiceBus;

namespace Keda.Worker.ConsumeEvents
{
    public class Worker : BackgroundService
    {
        public const string QueueName = "kedafila";
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connectionString = "<service_bus_connection_string>";
            var client = new ServiceBusClient(connectionString);

            var processor = client.CreateProcessor(QueueName);

            processor.ProcessMessageAsync += Processor_ProcessMessageAsync;
            processor.ProcessErrorAsync += Processor_ProcessErrorAsync;

            await processor
                    .StartProcessingAsync(stoppingToken)
                    .ConfigureAwait(false);
        }

        private Task Processor_ProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            return Task.CompletedTask;
        }

        private Task Processor_ProcessMessageAsync(ProcessMessageEventArgs arg)
        {
            var message = arg.Message.Body.ToString();

            _logger.LogInformation($"Message consumed: {message}");

            return Task.CompletedTask;
        }
    }
}
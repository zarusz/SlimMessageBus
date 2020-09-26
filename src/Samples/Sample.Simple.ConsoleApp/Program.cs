using System;
using System.Globalization;
using System.Threading.Tasks;
using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.Kafka;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using SecretStore;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Memory;
using Microsoft.Extensions.Logging;

namespace Sample.Simple.ConsoleApp
{
    enum Provider
    {
        Kafka,
        AzureServiceBus,
        AzureEventHub,
        Redis,
        Memory,
    }

    internal static class Program
    {
        private static readonly Random Random = new Random();
        private static bool _canRun = true;

        private static async Task Main()
        {
            // Load configuration
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            // Setup logger
            var loggerFactory = LoggerFactory.Create(cfg => cfg.AddConfiguration(configuration.GetSection("Logging")).AddConsole());

            // Local file with secrets
            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            // Create the bus and process messages
            using var messageBus = CreateMessageBus(configuration, loggerFactory);

            var addTask = Task.Factory.StartNew(() => AddLoop(messageBus), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var multiplyTask = Task.Factory.StartNew(() => MultiplyLoop(messageBus), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();

            _canRun = false;
            await Task.WhenAll(addTask, multiplyTask);
        }

        /**
         * Performs IMessageBus creation & configuration
         */
        private static IMessageBus CreateMessageBus(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            // Choose your provider
            var provider = Provider.Memory;

            // Provide your event hub-names OR kafka/service bus topic names
            var topicForAddCommand = "add-command";
            var topicForMultiplyRequest = "multiply-request";
            // Note: Each running instance (node) of ConsoleApp should have its own unique response queue/topic (i.e. responses-{n})
            var topicForResponses = "responses";
            // Provide consumer group name
            var consumerGroup = "consoleapp";
            var responseGroup = "consoleapp-1";

            /*
            
            Azure Event Hub setup notes:
              Create 3 event hubs in Azure:
                1. 'add-command' with 'consoleapp' group consumer
                2. 'multiply-request' with 'consoleapp' group consumer
                3. 'responses' with 'consoleapp-1' group consumer
            
            Azure Service Bus setup notes:
              Create 3 topics in Azure:
                1. 'add-command' with 'consoleapp' subscription
                2. 'multiply-request' with 'consoleapp' subscription
                3. 'responses' with 'consoleapp-1' subscription
            
             */

            // Create message bus using the fluent builder interface
            IMessageBus messageBus = MessageBusBuilder
                .Create()
                // Pub/Sub example
                .Produce<AddCommand>(x => x.DefaultTopic(topicForAddCommand)) // By default AddCommand messages will go to event-hub/topic named 'add-command'
                .Consume<AddCommand>(x => x
                    .Topic(topicForAddCommand)
                    .WithConsumer<AddCommandConsumer>()
                    //.WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.OnHandle))
                    //.WithConsumer<AddCommandConsumer>((consumer, message, name) => consumer.OnHandle(message, name))
                    .Group(consumerGroup) // for Apache Kafka & Azure Event Hub
                    .SubscriptionName(consumerGroup) // for Azure Service Bus
                )
                // Req/Resp example
                .Produce<MultiplyRequest>(x =>
                {
                    // By default AddCommand messages will go to topic/event-hub named 'multiply-request'
                    x.DefaultTopic(topicForMultiplyRequest);

                    if (provider == Provider.AzureServiceBus) // Azure SB specific
                    {
                        x.WithModifier((msg, sbMsg) =>
                        {
                            // Assign the Azure SB native message properties that you need
                            sbMsg.PartitionKey = (msg.Left * msg.Right).ToString(CultureInfo.InvariantCulture);
                            sbMsg.UserProperties["UserId"] = Guid.NewGuid();
                        });
                    }

                    if (provider == Provider.Kafka) // Kafka specific
                    {
                        // Message key could be set for the message (this is optional)
                        x.KeyProvider((request, topic) => Encoding.ASCII.GetBytes((request.Left + request.Right).ToString(CultureInfo.InvariantCulture)));
                        // Partition selector (this is optional) - assumptions that there are 2 partitions for the topic
                        // x.PartitionProvider((request, topic) => (request.Left + request.Right) % 2);
                    }
                })
                .Handle<MultiplyRequest, MultiplyResponse>(x => x
                    .Topic(topicForMultiplyRequest) // topic to expect the requests
                    .WithHandler<MultiplyRequestHandler>()
                    .Group(consumerGroup) // for Apache Kafka & Azure Event Hub
                    .SubscriptionName(consumerGroup) // for Azure Service Bus
                )
                // Configure response message queue (on topic) when using req/resp
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
                    x.Group(responseGroup);  // for Apache Kafka & Azure Event Hub
                    x.SubscriptionName(responseGroup); // for Azure Service Bus
                    x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
                })
                .WithLoggerFacory(loggerFactory)
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithDependencyResolver(new LookupDependencyResolver(type =>
                {
                    // Simulate a dependency container
                    if (type == typeof(AddCommandConsumer)) return new AddCommandConsumer();
                    if (type == typeof(MultiplyRequestHandler)) return new MultiplyRequestHandler();
                    throw new InvalidOperationException();
                }))
                .Do(builder =>
                {
                    Console.WriteLine($"Using {provider} as the transport provider");
                    switch (provider)
                    {
                        case Provider.Memory:
                            builder.WithProviderMemory(new MemoryMessageBusSettings()); // Use Azure Service Bus as provider
                            break;

                        case Provider.AzureServiceBus:
                            // Provide connection string to your Azure SB
                            var serviceBusConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

                            builder.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString)); // Use Azure Service Bus as provider
                            break;

                        case Provider.AzureEventHub:
                            // Provide connection string to your event hub namespace
                            var eventHubConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:ConnectionString"]);
                            var storageConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:Storage"]);
                            var storageContainerName = configuration["Azure:EventHub:ContainerName"];

                            builder.WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString, storageConnectionString, storageContainerName)); // Use Azure Event Hub as provider
                            break;

                        case Provider.Kafka:
                            // Ensure your Kafka broker is running
                            var kafkaBrokers = configuration["Kafka:Brokers"];
                            var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                            var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);

                            builder.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)); // Or use Apache Kafka as provider
                            break;

                        case Provider.Redis:
                            // Ensure your Kafka broker is running
                            var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                            builder.WithProviderRedis(new RedisMessageBusSettings(redisConnectionString)); // Or use Redis as provider
                            break;
                    }
                })
                .Build();
            return messageBus;
        }

        static async Task AddLoop(IMessageBus bus)
        {
            while (_canRun)
            {
                var a = Random.Next(100);
                var b = Random.Next(100);

                Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
                try
                {
                    await bus.Publish(new AddCommand { Left = a, Right = b });
                }
                catch (Exception)
                {
                    Console.WriteLine("Producer: publish error");
                }

                await Task.Delay(50); // Simulate some delay
            }
        }

        static async Task MultiplyLoop(IMessageBus bus)
        {
            while (_canRun)
            {
                var a = Random.Next(100);
                var b = Random.Next(100);

                Console.WriteLine("Sender: Sending numbers {0} and {1}", a, b);
                try
                {
                    var response = await bus.Send(new MultiplyRequest { Left = a, Right = b });
                    Console.WriteLine("Sender: Got response back with result {0}", response.Result);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Sender: request error or timeout: " + e);
                }

                await Task.Delay(50); // Simulate some work
            }
        }
    }

    public class AddCommand
    {
        public int Left { get; set; }
        public int Right { get; set; }
    }

    public class AddCommandConsumer : IConsumer<AddCommand>
    {
        #region Implementation of IConsumer<in AddCommand>

        public async Task OnHandle(AddCommand message, string name)
        {
            Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
            await Task.Delay(50); // Simulate some work
        }

        #endregion
    }

    public class MultiplyRequest : IRequestMessage<MultiplyResponse>
    {
        public int Left { get; set; }
        public int Right { get; set; }
    }

    public class MultiplyResponse
    {
        public int Result { get; set; }
    }

    public class MultiplyRequestHandler : IRequestHandler<MultiplyRequest, MultiplyResponse>
    {
        #region Implementation of IRequestHandler<in MultiplyRequest,MultiplyResponse>

        public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string name)
        {
            await Task.Delay(50); // Simulate some work
            return new MultiplyResponse { Result = request.Left * request.Right };
        }

        #endregion
    }

}

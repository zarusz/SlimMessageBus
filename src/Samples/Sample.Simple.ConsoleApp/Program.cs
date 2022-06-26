namespace Sample.Simple.ConsoleApp;

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using SecretStore;
using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Memory;
using Microsoft.Extensions.Logging;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using System.Reflection;
using SlimMessageBus.Host.MsDependencyInjection;

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
    private static readonly Random Random = new();
    private static bool _canRun = true;

    private static async Task Main()
    {
        // Load configuration
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        // Local file with secrets
        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        // Setup DI
        using var serviceProvider = new ServiceCollection()
            // Register MS logging
            .AddLogging(cfg => cfg.AddConfiguration(configuration.GetSection("Logging")).AddConsole())
            // Register bus
            .AddSlimMessageBus((mbb, services) =>
                {
                    ConfigureMessageBus(mbb, configuration, services);
                },
                // Option 1
                addConsumersFromAssembly: new[] { Assembly.GetExecutingAssembly() },
                addConfiguratorsFromAssembly: new[] { Assembly.GetExecutingAssembly() }
            )
            // Option 2
            //.AddMessageBusConsumersFromAssembly(Assembly.GetExecutingAssembly())
            //.AddMessageBusConfiguratorsFromAssembly(Assembly.GetExecutingAssembly())

            .BuildServiceProvider();

        // Create the bus and process messages
        using var messageBus = serviceProvider.GetRequiredService<IMessageBus>();

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
    private static void ConfigureMessageBus(MessageBusBuilder mbb, IConfiguration configuration, IServiceProvider services)
    {
        // Choose your provider
        var provider = Provider.Redis;

        // Provide your event hub-names OR kafka/service bus topic names
        var topicForAddCommand = "add-command";
        var topicForMultiplyRequest = "multiply-request";
        // Note: Each running instance (node) of ConsoleApp should have its own unique response queue/topic (i.e. responses-{n})
        var topicForResponses = "responses";
        // Provide consumer group name
        var consumerGroup = "consoleapp";
        var responseGroup = "consoleapp-1";

        if (provider == Provider.Kafka)
        {
            // Note: We are using the free plan of CloudKarafka to host the Kafka infrastructure. The free plan has a limit on topic you can get free and it requires these topic prefixes.
            topicForAddCommand = "4p5ma6io-test-ping";
            topicForMultiplyRequest = "4p5ma6io-multiply-request";
            topicForResponses = "4p5ma6io-responses";
        }

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
        mbb
            // Pub/Sub example
            .Produce<AddCommand>(x => x.DefaultTopic(topicForAddCommand)) // By default AddCommand messages will go to event-hub/topic named 'add-command'
            .Consume<AddCommand>(x => x.Topic(topicForAddCommand)
                                       .WithConsumer<AddCommandConsumer>()
                                       //.WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.OnHandle))
                                       //.WithConsumer<AddCommandConsumer>((consumer, message, name) => consumer.OnHandle(message, name))
                                       .KafkaGroup(consumerGroup) // for Apache Kafka & Azure Event Hub
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
                        sbMsg.ApplicationProperties["UserId"] = Guid.NewGuid();
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
                .KafkaGroup(consumerGroup) // for Apache Kafka & Azure Event Hub
                .SubscriptionName(consumerGroup) // for Azure Service Bus
            )
            // Configure response message queue (on topic) when using req/resp
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
                x.KafkaGroup(responseGroup);  // for Apache Kafka & Azure Event Hub
                x.SubscriptionName(responseGroup); // for Azure Service Bus
                x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
            })
            .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
            .PerMessageScopeEnabled(true) // Enable DI scope to be created for each message about to be processed
            .WithHeaderModifier((headers, message) =>
            {
                // Add additional headers for all outgoing messages
                headers["Source"] = "ConsoleApp";
            })
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

                        // If your cluster requires SSL
                        void AddSsl(ClientConfig c)
                        {
                            // cloudkarafka.com uses SSL with SASL authentication
                            c.SecurityProtocol = SecurityProtocol.SaslSsl;
                            c.SaslUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                            c.SaslPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);
                            c.SaslMechanism = SaslMechanism.ScramSha256;
                            c.SslCaLocation = "cloudkarafka_2020-12.ca";
                        }

                        builder.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)
                        {
                            //HeaderSerializer = new JsonMessageSerializer(),
                            ProducerConfig = (config) =>
                            {
                                AddSsl(config);
                            },
                            ConsumerConfig = (config) =>
                            {
                                config.AutoOffsetReset = AutoOffsetReset.Earliest;
                                AddSsl(config);
                            }

                        }); // Or use Apache Kafka as provider
                        break;

                    case Provider.Redis:
                        // Ensure your Kafka broker is running
                        var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                        builder.WithProviderRedis(new RedisMessageBusSettings(redisConnectionString) { EnvelopeSerializer = new MessageWithHeadersSerializer() }); // Or use Redis as provider
                        break;
                }
            });
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

public class AddCommandConsumer : IConsumer<AddCommand>, IConsumerWithContext
{
    public IConsumerContext Context { get; set; }

    public async Task OnHandle(AddCommand message, string name)
    {
        Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
        // Context.Headers -> has the headers
        await Task.Delay(50); // Simulate some work
    }
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
    public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string name)
    {
        await Task.Delay(50); // Simulate some work
        return new MultiplyResponse { Result = request.Left * request.Right };
    }
}

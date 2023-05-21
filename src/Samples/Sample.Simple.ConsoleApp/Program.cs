namespace Sample.Simple.ConsoleApp;

using System.Globalization;
using System.Reflection;
using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SecretStore;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization.Json;

enum Provider
{
    Kafka,
    AzureServiceBus,
    AzureEventHub,
    Redis,
    Memory,
}

/// <summary>
/// This sample is a console app that uses the .NET Generic Host https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host
/// </summary>
static internal class Program
{
    private static async Task Main(string[] args)
    {
        // Local file with secrets
        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, services) =>
            {
                services.AddHostedService<ApplicationService>();

                services.AddSlimMessageBus(mbb =>
                {
                    ConfigureMessageBus(mbb, ctx.Configuration);
                    mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
                    mbb.AddJsonSerializer();
                });
            })
            .Build()
            .RunAsync();
    }

    internal class ApplicationService : IHostedService
    {
        private readonly IMessageBus _messageBus;

        private readonly Random _random = new();
        private bool _canRun = true;

        // Note: Injecting IMessageBus will force MsDependencyInjection to eagerly load SMB consumers upon start.
        public ApplicationService(IMessageBus messageBus) => _messageBus = messageBus;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var addTask = Task.Factory.StartNew(AddLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var multiplyTask = Task.Factory.StartNew(MultiplyLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _canRun = false;
            return Task.CompletedTask;
        }

        private async Task AddLoop()
        {
            while (_canRun)
            {
                var a = _random.Next(100);
                var b = _random.Next(100);

                Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
                try
                {
                    await _messageBus.Publish(new AddCommand { Left = a, Right = b });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Producer: publish error {0}", ex.Message);
                }

                await Task.Delay(50); // Simulate some delay
            }
        }

        private async Task MultiplyLoop()
        {
            while (_canRun)
            {
                var a = _random.Next(100);
                var b = _random.Next(100);

                Console.WriteLine("Sender: Sending numbers {0} and {1}", a, b);
                try
                {
                    var response = await _messageBus.Send(new MultiplyRequest { Left = a, Right = b });
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

    /**
     * Performs IMessageBus creation & configuration
     */
    private static void ConfigureMessageBus(MessageBusBuilder mbb, IConfiguration configuration)
    {
        // Choose your provider
        var provider = Provider.Kafka;

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
            .Produce<AddCommand>(x => x.DefaultTopic(topicForAddCommand)
                                       .WithModifier((msg, nativeMsg) => nativeMsg.PartitionKey = msg.Left.ToString())) // By default AddCommand messages will go to event-hub/topic named 'add-command'
            .Consume<AddCommand>(x => x.Topic(topicForAddCommand)
                                       .WithConsumer<AddCommandConsumer>()
                                       //.WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.OnHandle))
                                       //.WithConsumer<AddCommandConsumer>((consumer, message, name) => consumer.OnHandle(message, name))
                                       .KafkaGroup(consumerGroup) // for Apache Kafka
                                       .EventHubGroup(consumerGroup) // for Azure Event Hub
                                                                     // for Azure Service Bus
                                       .SubscriptionName(consumerGroup)
                                       .SubscriptionSqlFilter("2=2")
                                       .CreateTopicOptions((options) =>
                                       {
                                           options.RequiresDuplicateDetection = true;
                                       })
            )
            // Req/Resp example
            .Produce<MultiplyRequest>(x =>
            {
                // By default AddCommand messages will go to topic/event-hub named 'multiply-request'
                x.DefaultTopic(topicForMultiplyRequest);
                x.CreateTopicOptions((options) =>
                {
                    options.EnablePartitioning = true;
                });

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
                .KafkaGroup(consumerGroup) // for Apache Kafka
                .EventHubGroup(consumerGroup) // for Azure Event Hub
                .SubscriptionName(consumerGroup) // for Azure Service Bus
            )
            // Configure response message queue (on topic) when using req/resp
            .ExpectRequestResponses(x =>
            {
                x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
                x.KafkaGroup(responseGroup); // for Apache Kafka
                x.EventHubGroup(responseGroup); // Azure Event Hub
                x.SubscriptionName(responseGroup); // for Azure Service Bus
                x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
            })
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
                        builder.WithProviderMemory(); // Use Memory as provider
                        break;

                    case Provider.AzureServiceBus:
                        // Provide connection string to your Azure SB
                        var serviceBusConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

                        // Use Azure Service Bus as provider
                        builder.WithProviderServiceBus(cfg =>
                        {
                            cfg.ConnectionString = serviceBusConnectionString;
                            cfg.TopologyProvisioning = new ServiceBusTopologySettings()
                            {
                                Enabled = true,
                                CreateQueueOptions = (options) =>
                                {
                                    options.EnablePartitioning = true;
                                    options.LockDuration = TimeSpan.FromMinutes(5);
                                },
                                CreateTopicOptions = (options) =>
                                {
                                    options.EnablePartitioning = true;
                                },
                                CreateSubscriptionOptions = (options) =>
                                {
                                    options.LockDuration = TimeSpan.FromMinutes(5);
                                }
                            };
                        });
                        break;

                    case Provider.AzureEventHub:
                        // Provide connection string to your event hub namespace
                        var eventHubConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:ConnectionString"]);
                        var storageConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:Storage"]);
                        var storageContainerName = configuration["Azure:EventHub:ContainerName"];

                        // Use Azure Event Hub as provider
                        builder.WithProviderEventHub(cfg =>
                        {
                            cfg.ConnectionString = eventHubConnectionString;
                            cfg.StorageConnectionString = storageConnectionString;
                            cfg.StorageBlobContainerName = storageContainerName;
                        });

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
                            c.SslCaLocation = "cloudkarafka_2022-10.ca";
                        }

                        // Or use Apache Kafka as provider
                        builder.WithProviderKafka(cfg =>
                        {
                            cfg.BrokerList = kafkaBrokers;
                            //HeaderSerializer = new JsonMessageSerializer(),
                            cfg.ProducerConfig = (config) =>
                            {
                                AddSsl(config);
                            };
                            cfg.ConsumerConfig = (config) =>
                            {
                                config.AutoOffsetReset = AutoOffsetReset.Earliest;
                                AddSsl(config);
                            };

                        });
                        break;

                    case Provider.Redis:
                        // Ensure your Kafka broker is running
                        var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                        // Or use Redis as provider
                        builder.WithProviderRedis(cfg =>
                        {
                            cfg.ConnectionString = redisConnectionString;
                        });
                        break;
                }
            });
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

    public async Task OnHandle(AddCommand message)
    {
        Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
        // Context.Headers -> has the headers
        await Task.Delay(50); // Simulate some work
    }
}

public class MultiplyRequest : IRequest<MultiplyResponse>
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
    public async Task<MultiplyResponse> OnHandle(MultiplyRequest request)
    {
        await Task.Delay(50); // Simulate some work
        return new MultiplyResponse { Result = request.Left * request.Right };
    }
}

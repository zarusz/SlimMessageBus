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
using SlimMessageBus.Host.AmazonSQS;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.AzureServiceBus;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization.Json;

/**
 * Performs IMessageBus creation & configuration
 */
void ConfigureMessageBus(MessageBusBuilder mbb, IConfiguration configuration)
{
    // Choose your transport
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
     */

    // Create message bus using the fluent builder interface
    mbb
        // Pub/Sub example
        .Produce<AddCommand>(x => x.DefaultTopic(topicForAddCommand)
                                   .WithModifier((msg, nativeMsg) => nativeMsg.PartitionKey = msg.Left.ToString())) // By default AddCommand messages will go to event-hub/topic named 'add-command'
        .Consume<AddCommand>(x => x.Topic(topicForAddCommand)
                                   //.WithConsumerOfContext<AddCommandConsumer>()
                                   //.WithConsumer<AddCommandConsumer>(nameof(AddCommandConsumer.OnHandle))
                                   //.WithConsumer<AddCommandConsumer>((consumer, message, name) => consumer.OnHandle(message, name))
                                   .KafkaGroup(consumerGroup) // for Apache Kafka
                                   .EventHubGroup(consumerGroup) // for Azure Event Hub                                                                     
                                   .SubscriptionName(consumerGroup) // for Azure Service Bus
                                   .SubscriptionSqlFilter("2=2") // for Azure Service Bus
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
                                            //.WithHandler<MultiplyRequestHandler>()
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
                        cfg.TopologyProvisioning = new()
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
                    var storageContainerName = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:ContainerName"]);

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
                    var kafkaBrokers = Secrets.Service.PopulateSecrets(configuration["Kafka:Brokers"]);
                    var kafkaSecure = Convert.ToBoolean(Secrets.Service.PopulateSecrets(configuration["Kafka:Secure"]));

                    // If your cluster requires SSL
                    void AddSsl(ClientConfig c)
                    {
                        // cloudkarafka.com uses SSL with SASL authentication
                        c.SecurityProtocol = SecurityProtocol.SaslSsl;
                        c.SaslUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                        c.SaslPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);
                        c.SaslMechanism = SaslMechanism.ScramSha256;
                        c.SslCaLocation = "cloudkarafka_2023-10.pem";
                    }

                    // Or use Apache Kafka as provider
                    builder.WithProviderKafka(cfg =>
                    {
                        cfg.BrokerList = kafkaBrokers;
                        //HeaderSerializer = new JsonMessageSerializer(),
                        cfg.ProducerConfig = (config) =>
                        {
                            if (kafkaSecure)
                            {
                                AddSsl(config);
                            }
                        };
                        cfg.ConsumerConfig = (config) =>
                        {
                            config.AutoOffsetReset = AutoOffsetReset.Earliest;
                            if (kafkaSecure)
                            {
                                AddSsl(config);
                            }
                        };

                    });
                    break;

                case Provider.Redis:
                    var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                    // Or use Redis as provider
                    builder.WithProviderRedis(cfg =>
                    {
                        cfg.ConnectionString = redisConnectionString;
                    });
                    break;

                case Provider.AmazonSQS:
                    var accessKey = Secrets.Service.PopulateSecrets(configuration["Amazon:AccessKey"]);
                    var secretAccess = Secrets.Service.PopulateSecrets(configuration["Amazon:SecretAccess"]);

                    // Or use Amazon SQS as provider
                    builder.WithProviderAmazonSQS(cfg =>
                    {
                        cfg.UseRegion(Amazon.RegionEndpoint.EUCentral1);
                        cfg.UseStaticCredentials(accessKey, secretAccess);
                    });
                    break;
            }
        });
}


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


internal class ApplicationService(IServiceProvider serviceProvider) : IHostedService
{
    private readonly Random _random = new();
    private bool _canRun = true;
    private IServiceScope _serviceScope;
    private IMessageBus _messageBus;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _serviceScope = serviceProvider.CreateScope();
        _messageBus = _serviceScope.ServiceProvider.GetService<IMessageBus>();

        var addTask = Task.Factory.StartNew(AddLoop, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var multiplyTask = Task.Factory.StartNew(MultiplyLoop, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _canRun = false;

        if (_serviceScope is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
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
                await _messageBus.Publish(new AddCommand(a, b));
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
                var response = await _messageBus.Send(new MultiplyRequest(a, b));
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

enum Provider
{
    Kafka,
    AzureServiceBus,
    AzureEventHub,
    Redis,
    Memory,
    AmazonSQS
}

public record AddCommand(int Left, int Right);

public class AddCommandConsumer : IConsumer<AddCommand>
{
    public async Task OnHandle(AddCommand message, CancellationToken cancellationToken)
    {
        Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
        // Context.Headers -> has the headers
        await Task.Delay(50, cancellationToken); // Simulate some work
    }
}

public record MultiplyRequest(int Left, int Right) : IRequest<MultiplyResponse>;

public record MultiplyResponse(int Result);

public class MultiplyRequestHandler : IRequestHandler<MultiplyRequest, MultiplyResponse>
{
    public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken); // Simulate some work
        return new(request.Left * request.Right);
    }
}

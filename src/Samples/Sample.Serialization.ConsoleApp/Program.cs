namespace Sample.Serialization.ConsoleApp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Sample.Serialization.MessagesAvro;

using SecretStore;

using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Serialization.Avro;
using SlimMessageBus.Host.Serialization.Hybrid;
using SlimMessageBus.Host.Serialization.Json;

enum Provider
{
    //Kafka,
    //AzureServiceBus,
    //AzureEventHub,
    Redis,
    Memory
}

/// <summary>
/// This sample shows:
/// 1. How tu use the Avro serializer (for contract Avro IDL first apprach to generate C# code)
/// 2. How to combine two serializer approaches in one app (using the Hybrid serializer).
/// </summary>
class Program
{
    static async Task Main(string[] args) => await Host.CreateDefaultBuilder(args)
        .ConfigureServices((ctx, services) =>
        {
            // Local file with secrets
            Secrets.Load(@"..\..\..\..\..\secrets.txt");

            services.AddHostedService<MainProgram>();
            services
                .AddSlimMessageBus(mbb =>
                {
                    // Note: remember that Memory provider does not support req-resp yet.
                    var provider = Provider.Memory;

                    mbb
                        .AddServicesFromAssemblyContaining<AddCommandConsumer>()

                        // Note: Certain messages will be serialized by the Avro serializer, others will fall back to the Json serializer (the default)
                        .AddHybridSerializer(builder =>
                        {
                            builder
                                .AsDefault()
                                .AddJsonSerializer();
                            
                            builder
                                .For(typeof(AddCommand), typeof(MultiplyRequest), typeof(MultiplyResponse))
                                .AddAvroSerializer();
                        })

                        .Produce<AddCommand>(x => x.DefaultTopic("AddCommand"))
                        .Consume<AddCommand>(x => x.Topic("AddCommand").WithConsumer<AddCommandConsumer>())

                        .Produce<SubtractCommand>(x => x.DefaultTopic("SubtractCommand"))
                        .Consume<SubtractCommand>(x => x.Topic("SubtractCommand").WithConsumer<SubtractCommandConsumer>())

                        .Produce<MultiplyRequest>(x => x.DefaultTopic("MultiplyRequest"))
                        .Handle<MultiplyRequest, MultiplyResponse>(x => x.Topic("MultiplyRequest").WithHandler<MultiplyRequestHandler>())

                        .ExpectRequestResponses(x => x.ReplyToTopic("ConsoleApp"))

                        .Do(builder =>
                        {
                            Console.WriteLine($"Using {provider} as the transport provider");
                            switch (provider)
                            {
                                case Provider.Memory:
                                    builder.WithProviderMemory(cfg => cfg.EnableMessageSerialization = true);
                                    break;

                                //case Provider.AzureServiceBus:
                                //    // Provide connection string to your Azure SB
                                //    var serviceBusConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:ServiceBus"]);

                                //    builder.WithProviderServiceBus(new ServiceBusMessageBusSettings(serviceBusConnectionString)); // Use Azure Service Bus as provider
                                //    break;

                                //case Provider.AzureEventHub:
                                //    // Provide connection string to your event hub namespace
                                //    var eventHubConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:ConnectionString"]);
                                //    var storageConnectionString = Secrets.Service.PopulateSecrets(configuration["Azure:EventHub:Storage"]);
                                //    var storageContainerName = configuration["Azure:EventHub:ContainerName"];

                                //    builder.WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString, storageConnectionString, storageContainerName)); // Use Azure Event Hub as provider
                                //    break;

                                //case Provider.Kafka:
                                //    // Ensure your Kafka broker is running
                                //    var kafkaBrokers = Secrets.Service.PopulateSecrets(configurationconfiguration["Kafka:Brokers"]);
                                //    var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                                //    var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);
                                //    var kafkaSecure = Secrets.Service.PopulateSecrets(configuration["Kafka:Secure"]);

                                //    builder.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)); // Or use Apache Kafka as provider
                                //    break;

                                case Provider.Redis:
                                    // Ensure your Redis broker is running
                                    // Or use Redis as provider
                                    builder.WithProviderRedis(cfg => cfg.ConnectionString = Secrets.Service.PopulateSecrets(ctx.Configuration["Redis:ConnectionString"]));
                                    break;

                                default:
                                    throw new NotSupportedException();
                            }
                        });
                });
        })
        .Build()
        .RunAsync();
}

public class MainProgram : IHostedService
{
    private readonly IMessageBus _bus;
    private readonly Random _random = new();
    private volatile bool _canRun = true;
    private Task _task;

    public MainProgram(IMessageBus bus) => _bus = bus;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var addTask = Task.Factory.StartNew(AddLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var multiplyTask = Task.Factory.StartNew(MultiplyLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        _task = Task.WhenAll(addTask, multiplyTask);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _canRun = false;
        await _task;
    }

    protected async Task AddLoop()
    {
        while (_canRun)
        {
            var a = _random.Next(100);
            var b = _random.Next(100);
            var opId = Guid.NewGuid().ToString();

            Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
            try
            {
                await _bus.Publish(new AddCommand { OperationId = opId, Left = a, Right = b });
                await _bus.Publish(new SubtractCommand { OperationId = opId, Left = a, Right = b });
            }
            catch (Exception e)
            {
                Console.WriteLine("Producer: publish error {0}", e);
            }

            await Task.Delay(50); // Simulate some delay
        }
    }

    protected async Task MultiplyLoop()
    {
        while (_canRun)
        {
            var a = _random.Next(100);
            var b = _random.Next(100);

            Console.WriteLine("Sender: Sending numbers {0} and {1}", a, b);
            try
            {
                var response = await _bus.Send(new MultiplyRequest { OperationId = Guid.NewGuid().ToString(), Left = a, Right = b });
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

public class AddCommandConsumer : IConsumer<AddCommand>
{
    public async Task OnHandle(AddCommand message)
    {
        Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
        await Task.Delay(50); // Simulate some work
    }
}

public class SubtractCommandConsumer : IConsumer<SubtractCommand>
{
    public async Task OnHandle(SubtractCommand message)
    {
        Console.WriteLine("Consumer: Subracting {0} and {1} gives {2}", message.Left, message.Right, message.Left - message.Right);
        await Task.Delay(50); // Simulate some work
    }
}

public class MultiplyRequestHandler : IRequestHandler<MultiplyRequest, MultiplyResponse>
{
    public async Task<MultiplyResponse> OnHandle(MultiplyRequest request)
    {
        await Task.Delay(50); // Simulate some work
        return new MultiplyResponse { Result = request.Left * request.Right, OperationId = request.OperationId };
    }
}

/// <summary>
/// This will be serialized as JSON.
/// </summary>
public class SubtractCommand
{
    public string OperationId { get; set; }
    public int Left { get; set; }
    public int Right { get; set; }
}
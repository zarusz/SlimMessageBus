namespace Sample.Serialization.ConsoleApp;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sample.Serialization.MessagesAvro;
using SecretStore;
using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization;
using SlimMessageBus.Host.Serialization.Avro;
using SlimMessageBus.Host.Serialization.Hybrid;
using SlimMessageBus.Host.Serialization.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    static async Task Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        // Local file with secrets
        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        using var bus = CreateBus(configuration);
        var program = new MainProgram(bus);

        await program.Run();
    }

    private static IMessageBus CreateBus(IConfiguration configuration)
    {
        // Note: remember that Memory provider does not support req-resp yet.
        var provider = Provider.Redis;

        /*
        var sl = new DictionarySchemaLookupStrategy();
        /// register all your types
        sl.Add(typeof(AddCommand), AddCommand._SCHEMA);
        sl.Add(typeof(MultiplyRequest), MultiplyRequest._SCHEMA);
        sl.Add(typeof(MultiplyResponse), MultiplyResponse._SCHEMA);

        var mf = new DictionaryMessageCreationStategy();
        /// register all your types
        mf.Add(typeof(AddCommand), () => new AddCommand());
        mf.Add(typeof(MultiplyRequest), () => new MultiplyRequest());
        mf.Add(typeof(MultiplyResponse), () => new MultiplyResponse());
        
        // longer approach, but should be faster as it's not using reflection
        var avroSerializer = new AvroMessageSerializer(mf, sl);
        */

        // alternatively a simpler approach, but using the slower ReflectionMessageCreationStategy and ReflectionSchemaLookupStrategy
        var avroSerializer = new AvroMessageSerializer(NullLoggerFactory.Instance);

        // Avro serialized using the AvroConvert library - no schema generation neeeded upfront.
        var jsonSerializer = new JsonMessageSerializer();

        // Note: Certain messages will be serialized by one Avro serializer, other using the Json serializer
        var routingSerializer = new HybridMessageSerializer(new Dictionary<IMessageSerializer, Type[]>
        {
            [jsonSerializer] = new[] { typeof(SubtractCommand) }, // the first one will be the default serializer, no need to declare types here
            [avroSerializer] = new[] { typeof(AddCommand), typeof(MultiplyRequest), typeof(MultiplyResponse) },
        }, NullLogger<HybridMessageSerializer>.Instance);

        return MessageBusBuilder.Create()
            .Produce<AddCommand>(x => x.DefaultTopic("AddCommand"))
            .Consume<AddCommand>(x => x.Topic("AddCommand").WithConsumer<AddCommandConsumer>())

            .Produce<SubtractCommand>(x => x.DefaultTopic("SubtractCommand"))
            .Consume<SubtractCommand>(x => x.Topic("SubtractCommand").WithConsumer<SubtractCommandConsumer>())

            .Produce<MultiplyRequest>(x => x.DefaultTopic("MultiplyRequest"))
            .Handle<MultiplyRequest, MultiplyResponse>(x => x.Topic("MultiplyRequest").WithHandler<MultiplyRequestHandler>())


            .ExpectRequestResponses(x => x.ReplyToTopic("ConsoleApp"))

            .WithSerializer(routingSerializer) // Use Avro for message serialization                
            .WithDependencyResolver(new LookupDependencyResolver(type =>
            {
                // Simulate a dependency container
                if (type == typeof(AddCommandConsumer)) return new AddCommandConsumer();
                if (type == typeof(SubtractCommandConsumer)) return new SubtractCommandConsumer();
                if (type == typeof(MultiplyRequestHandler)) return new MultiplyRequestHandler();
                if (type == typeof(ILoggerFactory)) return NullLoggerFactory.Instance;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return Enumerable.Empty<object>();
                throw new InvalidOperationException();
            }))
            .Do(builder =>
            {
                Console.WriteLine($"Using {provider} as the transport provider");
                switch (provider)
                {
                    case Provider.Memory:
                        builder.WithProviderMemory(new MemoryMessageBusSettings { EnableMessageSerialization = true });
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
                    //    var kafkaBrokers = configuration["Kafka:Brokers"];
                    //    var kafkaUsername = Secrets.Service.PopulateSecrets(configuration["Kafka:Username"]);
                    //    var kafkaPassword = Secrets.Service.PopulateSecrets(configuration["Kafka:Password"]);

                    //    builder.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)); // Or use Apache Kafka as provider
                    //    break;

                    case Provider.Redis:
                        // Ensure your Redis broker is running
                        var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                        builder.WithProviderRedis(new RedisMessageBusSettings(redisConnectionString)); // Or use Redis as provider
                        break;

                    default:
                        throw new NotSupportedException();
                }
            })
            .Build();
    }
}

public class MainProgram
{
    private readonly IMessageBus _bus;
    private readonly Random _random = new Random();
    private volatile bool _canRun = true;

    public MainProgram(IMessageBus bus)
    {
        _bus = bus;
    }

    public async Task Run()
    {
        var addTask = Task.Factory.StartNew(AddLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var multiplyTask = Task.Factory.StartNew(MultiplyLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();

        _canRun = false;

        await Task.WhenAll(addTask);
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
    public async Task OnHandle(AddCommand message, string name)
    {
        Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
        await Task.Delay(50); // Simulate some work
    }
}

public class SubtractCommandConsumer : IConsumer<SubtractCommand>
{
    public async Task OnHandle(SubtractCommand message, string name)
    {
        Console.WriteLine("Consumer: Subracting {0} and {1} gives {2}", message.Left, message.Right, message.Left - message.Right);
        await Task.Delay(50); // Simulate some work
    }
}

public class MultiplyRequestHandler : IRequestHandler<MultiplyRequest, MultiplyResponse>
{
    public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string name)
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
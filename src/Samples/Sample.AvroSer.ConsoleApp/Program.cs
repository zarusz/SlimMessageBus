using Sample.AvroSer.Messages;
using SlimMessageBus;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.Avro;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Avro.ConsoleApp
{
    enum Provider
    {
        Kafka,
        AzureServiceBus,
        AzureEventHub,
        Redis,
        Memory
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            using var bus = CreateBus();
            var program = new MainProgram(bus);

            await program.Run();
        }

        private static IMessageBus CreateBus()
        {
            var provider = Provider.Memory;

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

            var avroSerializer = new AvroMessageSerializer(mf, sl);
            
            // alternatively a simpler approach, but using the slower ReflectionMessageCreationStategy and ReflectionSchemaLookupStrategy
            //var avroSerializer = new AvroMessageSerializer(); 

            return MessageBusBuilder.Create()
                .Produce<AddCommand>(x => x.DefaultTopic("AddCommand"))
                .Consume<AddCommand>(x => x.Topic("AddCommand").WithConsumer<AddCommandConsumer>())
                .WithSerializer(avroSerializer) // Use Avro for message serialization                
                .WithDependencyResolver(new LookupDependencyResolver(type =>
                {
                    // Simulate a dependency container
                    if (type == typeof(AddCommandConsumer)) return new AddCommandConsumer();
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

                        default:
                            throw new NotSupportedException();

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

                        //case Provider.Redis:
                        //    // Ensure your Kafka broker is running
                        //    var redisConnectionString = Secrets.Service.PopulateSecrets(configuration["Redis:ConnectionString"]);

                        //    builder.WithProviderRedis(new RedisMessageBusSettings(redisConnectionString)); // Or use Redis as provider
                        //    break;
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

                Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
                try
                {
                    await _bus.Publish(new AddCommand { OperationId = Guid.NewGuid().ToString("N"), Left = a, Right = b });
                }
                catch (Exception e)
                {
                    Console.WriteLine("Producer: publish error {0}", e);
                }

                await Task.Delay(50); // Simulate some delay
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
}
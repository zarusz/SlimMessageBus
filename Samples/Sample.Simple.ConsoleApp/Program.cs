using System;
using System.Threading.Tasks;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.Kafka;

namespace Sample.Simple.ConsoleApp
{
    class Program
    {
        static readonly Random Random = new Random();
        static bool _stop;

        public const int ConsumerDelayMs = 500;

        static void Main(string[] args)
        {
            // ToDo: Provider your event hub names
            var eventHubNameForAddCommand = "add-command";
            var eventHubNameForMultiplyRequest = "multiply-request";
            var eventHubNameForResponses = "responses";
            // ToDo: Provide consumer group name
            var consumerGroup = "consoleapp";

            // ToDo: Provide connection string to your event hub namespace
            var eventHubConnectionString = "";
            // ToDo: Provide connection string to your storage account 
            var storageConnectionString = "";

            /*
            Azure setup notes:
              1. Remember to create 3 event hubs in Azure:
                add-command
                multiply-request
                responses

              2. Remember to create 'consoleapp' group consumer in each event hub.
            */

            // Create message bus
            IMessageBus messageBus = new MessageBusBuilder()
                // Pub / Sub
                .Publish<AddCommand>(x => x.DefaultTopic(eventHubNameForAddCommand)) // By default messages of type 'AddCommand' will go to event hub named 'topica' (or topic if Kafka is chosen)
                .SubscribeTo<AddCommand>(x => x.Topic(eventHubNameForAddCommand).Group(consumerGroup).WithSubscriber<AddCommandConsumer>())
                // Req / Resp
                .Publish<MultiplyRequest>(x => x.DefaultTopic(eventHubNameForMultiplyRequest)) // By default messages of type 'AddCommand' will go to event hub named 'topica' (or topic if Kafka is chosen)
                .Handle<MultiplyRequest, MultiplyResponse>(x => x.Topic(eventHubNameForMultiplyRequest).Group(consumerGroup).WithHandler<MultiplyRequestHandler>())
                .ExpectRequestResponses(x =>
                {
                    x.Group("consoleapp");                    
                    x.ReplyToTopic(eventHubNameForResponses); // All responses from req/resp will return on this topic (event hub)
                })
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithDependencyResolver(new LookupDependencyResolver(type =>
                {
                    if (type == typeof (AddCommandConsumer)) return new AddCommandConsumer();
                    if (type == typeof (MultiplyRequestHandler)) return new MultiplyRequestHandler();
                    throw new InvalidOperationException();
                }))
                .WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString, storageConnectionString)) // Use Azure Event Hub as provider
                //.WithProviderKafka(new KafkaMessageBusSettings("<kafka-broker-list-here>")) // Or use Apache Kafka as provider
                .Build();

            try
            {
                var addTask = Task.Factory.StartNew(() => AddLoop(messageBus), TaskCreationOptions.LongRunning);
                var multiplyTask = Task.Factory.StartNew(() => MultiplyLoop(messageBus), TaskCreationOptions.LongRunning);

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();

                _stop = true;
                Task.WaitAll(addTask, multiplyTask);
            }
            finally
            {
                messageBus.Dispose();
            }
        }

        static async Task AddLoop(IMessageBus bus)
        {
            while (!_stop)
            {
                var a = Random.Next(100);
                var b = Random.Next(100);

                Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
                await bus.Publish(new AddCommand { Left = a, Right = b });

                await Task.Delay(50); // Simulate some delay
            }
        }

        static async Task MultiplyLoop(IMessageBus bus)
        {
            while (!_stop)
            {
                var a = Random.Next(100);
                var b = Random.Next(100);

                Console.WriteLine("Sender: Sending numbers {0} and {1}", a, b);
                var response = await bus.Send(new MultiplyRequest { Left = a, Right = b });
                Console.WriteLine("Sender: Got result back {0}", response.Result);

                await Task.Delay(50); // Simulate some delay
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

        public async Task OnHandle(AddCommand message, string topic)
        {
            await Task.Delay(Program.ConsumerDelayMs);
            Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
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

        public async Task<MultiplyResponse> OnHandle(MultiplyRequest request, string topic)
        {
            await Task.Delay(Program.ConsumerDelayMs);
            return new MultiplyResponse { Result = request.Left * request.Right };
        }

        #endregion
    }

}

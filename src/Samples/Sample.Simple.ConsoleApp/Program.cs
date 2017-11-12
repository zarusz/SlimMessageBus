using System;
using System.Threading.Tasks;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.AzureEventHub;
using SlimMessageBus.Host.Kafka;
using System.Text;

namespace Sample.Simple.ConsoleApp
{
    class Program
    {
        static readonly Random Random = new Random();
        static bool _stop;

        static void Main(string[] args)
        {
            // ToDo: Provider your event hub names
            var topicForAddCommand = "add-command";
            var topicForMultiplyRequest = "multiply-request";            
            // Note: Each running instance (node) of ConsoleApp should have its own unique response queue (i.e. responses-1)
            var topicForResponses = "responses";
            // ToDo: Provide consumer group name
            var consumerGroup = "consoleapp";
            var responseGroup = "consoleapp-1";

            // ToDo: Provide connection string to your event hub namespace
            var eventHubConnectionString = "";
            // ToDo: Provide connection string to your storage account 
            var storageConnectionString = "";

            // ToDo: Ensure your Kafka broker is running
            var kafkaBrokers = "localhost:9092";

            /*
            Azure setup notes:
              Create 3 event hubs in Azure:
                1. 'add-command' with 'consoleapp' group consumer
                2. 'multiply-request' with 'consoleapp' group consumer
                3. 'responses' with 'consoleapp-1' group consumer
            */

            // Create message bus using the fluent builder interface
            IMessageBus messageBus = new MessageBusBuilder()
                // Pub/Sub example
                .Publish<AddCommand>(x => x.DefaultTopic(topicForAddCommand)) // By default AddCommand messages will go to event hub named 'add-command' (or topic if Kafka is chosen)
                .SubscribeTo<AddCommand>(x => x.Topic(topicForAddCommand)
                                               .Group(consumerGroup)
                                               .WithSubscriber<AddCommandConsumer>())
                // Req/Resp example
                .Publish<MultiplyRequest>(x => 
                {
                    // By default AddCommand messages will go to event hub named 'multiply-request' (or topic if Kafka is chosen)
                    x.DefaultTopic(topicForMultiplyRequest);
                    // Message key could be set for the message
                    x.KeyProvider((request, topic) => Encoding.ASCII.GetBytes((request.Left + request.Right).ToString()));
                })
                .Handle<MultiplyRequest, MultiplyResponse>(x => x.Topic(topicForMultiplyRequest) // topic to expect the requests
                                                                 .Group(consumerGroup)
                                                                 .WithHandler<MultiplyRequestHandler>())
                // Configure response message queue (on topic) when using req/resp
                .ExpectRequestResponses(x =>
                {
                    x.Group(responseGroup); 
                    x.ReplyToTopic(topicForResponses); // All responses from req/resp will return on this topic (the EventHub name)
                    x.DefaultTimeout(TimeSpan.FromSeconds(20)); // Timeout request sender if response won't arrive within 10 seconds.
                })
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithDependencyResolver(new LookupDependencyResolver(type =>
                {
                    // Simulate a dependency container
                    if (type == typeof (AddCommandConsumer)) return new AddCommandConsumer();
                    if (type == typeof (MultiplyRequestHandler)) return new MultiplyRequestHandler();
                    throw new InvalidOperationException();
                }))
                //.WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString, storageConnectionString)) // Use Azure Event Hub as provider
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers)) // Or use Apache Kafka as provider
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
            while (!_stop)
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
            // simulate some processing
            return new MultiplyResponse { Result = request.Left * request.Right };
        }

        #endregion
    }

}

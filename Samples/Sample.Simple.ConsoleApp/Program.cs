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

        static void Main(string[] args)
        {
            // ToDo: Provide connection string to your event hub namespace
            var eventHubConnectionString = "";
            // ToDo: Provider your event hub name
            var eventHubName = "topica";
            // ToDo: Provide connection string to your storage account 
            var storageConnectionString = "";

            // Create message bus
            IMessageBus messageBus = new MessageBusBuilder()
                .Publish<AddCommand>(x => x.DefaultTopic(eventHubName)) // By default messages of type 'AddCommand' will go to event hub named 'topica' (or topic of Kafka is chosen)
                .SubscribeTo<AddCommand>(x => x.Topic(eventHubName).Group("consoleapp").WithSubscriber<AddCommandHandler>().Instances(1))
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithDependencyResolver(new LookupDependencyResolver(type => type == typeof(AddCommandHandler) ? new AddCommandHandler() : null))
                .WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString, storageConnectionString)) // Use Azure Event Hub
                //.WithProviderKafka(new KafkaMessageBusSettings("<kafka-broker-list-here>")) // Or use Apache Kafka
                .Build();

            try
            {
                var producerTask = Task.Factory.StartNew(() => ProducerLoop(messageBus), TaskCreationOptions.LongRunning);

                Console.WriteLine("Press any key to stop...");
                Console.ReadKey();

                _stop = true;
                producerTask.Wait();
            }
            finally
            {
                messageBus.Dispose();
            }
        }

        static async Task ProducerLoop(IMessageBus bus)
        {
            while (!_stop)
            {
                var a = Random.Next(100);
                var b = Random.Next(100);

                Console.WriteLine("Producer: Sending numbers {0} and {1}", a, b);
                await bus.Publish(new AddCommand(a, b));
                await Task.Delay(50);
            }
        }
    }

    public class AddCommand
    {
        public int Left { get; set; }
        public int Right { get; set; }

        public AddCommand(int left, int right)
        {
            Left = left;
            Right = right;
        }
    }

    public class AddCommandHandler : IConsumer<AddCommand>
    {
        #region Implementation of IConsumer<in AddCommand>

        public Task OnHandle(AddCommand message, string topic)
        {
            Console.WriteLine("Consumer: Adding {0} and {1} gives {2}", message.Left, message.Right, message.Left + message.Right);
            return Task.FromResult(0);
        }

        #endregion
    }
}

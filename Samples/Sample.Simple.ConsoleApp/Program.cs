using System;
using System.Threading.Tasks;
using SlimMessageBus;
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
            var eventHubConnectionString = "Endpoint=sb://slimmessagebus.servicebus.windows.net/;SharedAccessKeyName=AppAccessKey;SharedAccessKey=mxendUq7rHnAsUBP1xfuaK8bsRJA69jy6bOUI1Q1iNI=";
            var eventHubName = "topica";

            // Create message bus
            IMessageBus messageBus = new MessageBusBuilder()
                .Publish<AddCommand>(x => x.DefaultTopic(eventHubName)) // By default messages of type AddCommand will go 'topica' Event Hub path (or Kafka topic)
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithProviderEventHub(new EventHubMessageBusSettings(eventHubConnectionString)) // Use Azure Event Hub
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

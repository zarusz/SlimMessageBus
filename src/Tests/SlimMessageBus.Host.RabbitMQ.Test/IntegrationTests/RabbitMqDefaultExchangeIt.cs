namespace SlimMessageBus.Host.RabbitMQ.Test.IntegrationTests;

using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.Test.Common.IntegrationTest;

[Trait("Category", "Integration")]
[Trait("Transport", "RabbitMQ")]
public class RabbitMqDefaultExchangeIt(ITestOutputHelper output) : BaseIntegrationTest<RabbitMqDefaultExchangeIt>(output)
{
    [Fact]
    public async Task PublishDirectlyToQueueUsingDefaultExchange()
    {
        const string queueName = "default-exchange-queue";

        AddBusConfiguration(mbb =>
        {
            mbb.Produce<PingMessage>(x => x
               .DefaultPath(string.Empty)
               .RoutingKeyProvider((m, ctx) => queueName)
               .MessagePropertiesModifier((m, p) =>
               {
                   p.MessageId = $"ID_{m.Counter}";
                   p.ContentType = MediaTypeNames.Application.Json;
               }));

            mbb.Consume<PingMessage>(x => x
                .Path(string.Empty) // default exchange
                .Queue(queueName)
                .WithConsumer<PingConsumer>());
        });

        var messageBus = ServiceProvider.GetRequiredService<IMessageBus>();
        var consumedMessages = ServiceProvider.GetRequiredService<TestEventCollector<TestEvent>>();

        var ping = new PingMessage { Counter = 42 };

        // act
        await messageBus.Publish(ping);

        await consumedMessages.WaitUntilArriving();

        // assert
        var received = consumedMessages.Snapshot().Single();
        received.Message.Counter.Should().Be(42);
        received.Message.Value.Should().Be(ping.Value);
    }

    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus((mbb) =>
        {
            mbb.WithProviderRabbitMQ(cfg =>
            {
                cfg.ConnectionString = Secrets.Service.PopulateSecrets(configuration["RabbitMQ:ConnectionString"]);

                cfg.ConnectionFactory.ClientProvidedName = $"MyService_{Environment.MachineName}";

                cfg.UseMessagePropertiesModifier((m, p) =>
                {
                    p.ContentType = MediaTypeNames.Application.Json;
                });
                cfg.UseQueueDefaults(durable: false);
                cfg.UseTopologyInitializer((channel, applyDefaultTopology) =>
                {
                    // before test clean up
                    channel.QueueDelete("default-exchange-queue", ifUnused: true, ifEmpty: false);

                    // apply default SMB inferred topology
                    applyDefaultTopology();

                    // after
                });
            });
            mbb.AddServicesFromAssemblyContaining<PingConsumer>();
            mbb.AddJsonSerializer();
            ApplyBusConfiguration(mbb);
        });

        // Custom error handler
        services.AddTransient(typeof(IRabbitMqConsumerErrorHandler<>), typeof(CustomRabbitMqConsumerErrorHandler<>));

        services.AddSingleton<TestEventCollector<TestEvent>>();
    }
}

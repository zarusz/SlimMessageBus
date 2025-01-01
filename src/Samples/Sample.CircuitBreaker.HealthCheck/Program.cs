namespace Sample.CircuitBreaker.HealthCheck;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Sample.CircuitBreaker.HealthCheck.HealthChecks;

using SlimMessageBus.Host.CircuitBreaker.HealthCheck;

public static class Program
{
    private static async Task Main(string[] args)
    {
        // Local file with secrets
        Secrets.Load(@"..\..\..\..\..\secrets.txt");

        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((builder, services) =>
            {
                const string AddTag = "add";
                const string SubtractTag = "subtract";

                services.AddSlimMessageBus(mbb =>
                {
                    var ticks = DateTimeOffset.UtcNow.Ticks;
                    var addTopic = $"Sample-CircuitBreaker-HealthCheck-add-{ticks}";
                    var subtractTopic = $"Sample-CircuitBreaker-HealthCheck-subtract-{ticks}";

                    mbb
                        .WithProviderRabbitMQ(
                            cfg =>
                            {
                                cfg.ConnectionString = Secrets.Service.PopulateSecrets(builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString"));
                                cfg.ConnectionFactory.ClientProvidedName = $"Sample_CircuitBreaker_HealthCheck_{Environment.MachineName}";

                                cfg.UseMessagePropertiesModifier((m, p) => p.ContentType = MediaTypeNames.Application.Json);
                                cfg.UseExchangeDefaults(durable: false);
                                cfg.UseQueueDefaults(durable: false);
                            });
                    mbb
                        .Produce<Add>(x => x
                            .Exchange(addTopic, exchangeType: ExchangeType.Fanout, autoDelete: false)
                            .RoutingKeyProvider((m, p) => Guid.NewGuid().ToString()))
                        .Consume<Add>(
                            cfg =>
                            {
                                cfg
                                    .Queue(nameof(Add), autoDelete: false)
                                    .Path(nameof(Add))
                                    .ExchangeBinding(addTopic)
                                    .WithConsumer<AddConsumer>()
                                    .PauseOnDegradedHealthCheck(AddTag);
                            });

                    mbb
                        .Produce<Subtract>(x => x
                            .Exchange(subtractTopic, exchangeType: ExchangeType.Fanout, autoDelete: false)
                            .RoutingKeyProvider((m, p) => Guid.NewGuid().ToString()))
                        .Consume<Subtract>(
                            cfg =>
                            {
                                cfg
                                    .Queue(nameof(Subtract), autoDelete: false)
                                    .Path(nameof(Subtract))
                                    .ExchangeBinding(subtractTopic)
                                    .WithConsumer<SubtractConsumer>()
                                    .PauseOnUnhealthyCheck(SubtractTag);
                            });

                    mbb.AddServicesFromAssembly(Assembly.GetExecutingAssembly());
                    mbb.AddJsonSerializer();
                });

                services.AddHostedService<IntermittentMessagePublisher>();
                services.AddSingleton<AddRandomHealthCheck>();
                services.AddSingleton<SubtractRandomHealthCheck>();

                services.Configure<HealthCheckPublisherOptions>(cfg =>
                {
                    // aggressive to toggle health status often (sample only)
                    cfg.Delay = TimeSpan.FromSeconds(3);
                    cfg.Period = TimeSpan.FromSeconds(5);
                });

                services
                    .AddHealthChecks()
                    .AddCheck<AddRandomHealthCheck>("Add", tags: [AddTag])
                    .AddCheck<SubtractRandomHealthCheck>("Subtract", tags: [SubtractTag]);
            })
            .Build()
            .RunAsync();
    }
}

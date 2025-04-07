namespace SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.Fixtures;

public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer;

    public RabbitMqFixture()
    {
        var builder = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12.14-management-alpine");

        _rabbitMqContainer = builder.Build();
    }

    public string GetConnectionString()
    {
        return _rabbitMqContainer.GetConnectionString();
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _rabbitMqContainer.DisposeAsync();
    }
}
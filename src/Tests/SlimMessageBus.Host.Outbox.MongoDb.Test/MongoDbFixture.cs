namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

using System.Text.RegularExpressions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

using Testcontainers.MongoDb;

public partial class MongoDbFixture : IAsyncLifetime
{
    [GeneratedRegex("waiting for connections", RegexOptions.IgnoreCase)]
    private static partial Regex WaitingForConnectionsRegex();

    private readonly MongoDbContainer _mongoDbContainer;

    public MongoDbFixture()
    {
        // mongo:8.0 uses structured JSON logging by default. The built-in wait
        // strategy looks for "Waiting for connections" as a substring which is
        // still present in the JSON log output ("msg":"Waiting for connections").
        // Use a case-insensitive regex to handle both log formats.
        var builder = new MongoDbBuilder("mongo:8.0")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged(WaitingForConnectionsRegex()))
            .WithReuse(true);

        if (Debugger.IsAttached)
        {
            builder = builder.WithPortBinding(27018, 27017);
        }

        _mongoDbContainer = builder.Build();
    }

    public string GetConnectionString() => _mongoDbContainer.GetConnectionString();

    public IMongoClient CreateClient() => new MongoClient(GetConnectionString());

    public async Task InitializeAsync()
    {
        await _mongoDbContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _mongoDbContainer.DisposeAsync();
    }
}

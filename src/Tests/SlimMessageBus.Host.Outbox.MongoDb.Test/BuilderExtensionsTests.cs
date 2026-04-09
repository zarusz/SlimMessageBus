namespace SlimMessageBus.Host.Outbox.MongoDb.Test;

using SlimMessageBus.Host;
using MongoDbBuilderExt = SlimMessageBus.Host.Outbox.MongoDb.Configuration.BuilderExtensions;

public class BuilderExtensionsTests
{
    [Fact]
    public void When_UseMongoDbTransaction_Given_MessageBusBuilderAndEnabled_Then_SetsEnabledProperty()
    {
        var builder = MessageBusBuilder.Create();

        builder.UseMongoDbTransaction(enabled: true);

        builder.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionEnabled].Should().Be(true);
        builder.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionFilter].Should().BeNull();
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_MessageBusBuilderAndDisabled_Then_SetsEnabledFalse()
    {
        var builder = MessageBusBuilder.Create();

        builder.UseMongoDbTransaction(enabled: false);

        builder.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionEnabled].Should().Be(false);
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_MessageBusBuilderAndMessageTypeFilter_Then_SetsFilterProperty()
    {
        var builder = MessageBusBuilder.Create();
        Func<Type, bool> filter = t => t == typeof(string);

        builder.UseMongoDbTransaction(messageTypeFilter: filter);

        builder.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionFilter].Should().BeSameAs(filter);
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_MessageBusBuilder_Then_ReturnsSameBuilder()
    {
        var builder = MessageBusBuilder.Create();

        var result = builder.UseMongoDbTransaction();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_ConsumerBuilder_Then_SetsEnabledPropertyOnBusSettings()
    {
        // ConsumerBuilder.Settings returns the parent bus settings (not consumer-specific settings),
        // so the flag ends up on the bus-level Properties dictionary.
        var consumer = new ConsumerBuilder<SampleMessage>(new MessageBusSettings());

        consumer.UseMongoDbTransaction(enabled: true);

        consumer.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionEnabled].Should().Be(true);
        consumer.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionFilter].Should().BeNull();
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_ConsumerBuilder_Then_ReturnsSameBuilder()
    {
        var consumer = new ConsumerBuilder<SampleMessage>(new MessageBusSettings());

        var result = consumer.UseMongoDbTransaction();

        result.Should().BeSameAs(consumer);
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_HandlerBuilder_Then_SetsEnabledPropertyOnBusSettings()
    {
        var handler = new HandlerBuilder<SampleRequest, SampleResponse>(new MessageBusSettings());

        handler.UseMongoDbTransaction(enabled: true);

        handler.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionEnabled].Should().Be(true);
        handler.Settings.Properties[MongoDbBuilderExt.PropertyMongoDbTransactionFilter].Should().BeNull();
    }

    [Fact]
    public void When_UseMongoDbTransaction_Given_HandlerBuilder_Then_ReturnsSameBuilder()
    {
        var handler = new HandlerBuilder<SampleRequest, SampleResponse>(new MessageBusSettings());

        var result = handler.UseMongoDbTransaction();

        result.Should().BeSameAs(handler);
    }

    public record SampleMessage;
    public record SampleRequest;
    public record SampleResponse;
}

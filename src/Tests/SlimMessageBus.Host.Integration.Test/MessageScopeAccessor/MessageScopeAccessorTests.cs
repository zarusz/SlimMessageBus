namespace SlimMessageBus.Host.Integration.Test.MessageScopeAccessor;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Memory;

/// <summary>
/// This test verifies that the <see cref="IMessageScopeAccessor"/> correctly looks up the <see cref="IServiceProvider"/> for the current message scope.
/// </summary>
/// <param name="output"></param>
[Trait("Category", "Integration")]
public class MessageScopeAccessorTests(ITestOutputHelper output) : BaseIntegrationTest<MessageScopeAccessorTests>(output)
{
    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("Memory", builder =>
            {
                builder
                    .WithProviderMemory()
                    .AutoDeclareFrom(Assembly.GetExecutingAssembly(), t => t.Namespace.Contains("MessageScopeAccessor"))
                    .PerMessageScopeEnabled();
            });
        });
        services.AddScoped<TestValueHolder>();
    }

    [Fact]
    public async Task Given_MemoryConsumer_When_MessageScopeAccessorCalledInsideConsumer_Then_LooksUpInTheMessageScope()
    {
        // Arrange
        using var scope = ServiceProvider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var value = Guid.NewGuid();

        // Act
        await bus.Publish(new TestMessage(value));

        // Assert
    }

    public record TestMessage(Guid Value);

    public class TestMessageConsumer(IServiceProvider serviceProvider,
                                     IMessageScopeAccessor messageScopeAccessor,
                                     IConsumerContext consumerContext)
        : IRequestHandler<TestMessage>, IConsumerWithContext
    {
        public required IConsumerContext Context { get; set; }

        public Task OnHandle(TestMessage request, CancellationToken cancellationToken)
        {
            // The ServcieProvider should match what is reported by IMessageScopeAccessor
            serviceProvider.Should().BeSameAs(messageScopeAccessor.Current);

            // The consumer context set should match what is injected
            Context.Should().BeSameAs(consumerContext);
            Context.Should().BeSameAs(serviceProvider.GetRequiredService<IConsumerContext>());


            return Task.CompletedTask;
        }
    }

    public class TestValueHolder
    {
        public IServiceProvider ServiceProvider { get; set; }
        public IServiceProvider MessageScopeAccessorServiceProvider { get; set; }
        public IConsumerContext ConsumerContextSetter { get; set; }
        public IConsumerContext ConsumerContextInjected { get; set; }
    }

}

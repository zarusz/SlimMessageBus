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
            mbb.AddServicesFromAssemblyContaining<TestMessageConsumer>();
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
        var holder = scope.ServiceProvider.GetRequiredService<TestValueHolder>();
        holder.ServiceProvider.Should().BeSameAs(holder.MessageScopeAccessorServiceProvider);
    }

    public record TestMessage(Guid Value);

    public class TestMessageConsumer(TestValueHolder holder, IServiceProvider serviceProvider, IMessageScopeAccessor messageScopeAccessor) : IRequestHandler<TestMessage>
    {
        public Task OnHandle(TestMessage request, CancellationToken cancellationToken)
        {
            holder.ServiceProvider = serviceProvider;
            holder.MessageScopeAccessorServiceProvider = messageScopeAccessor.Current;
            return Task.CompletedTask;
        }
    }

    public class TestValueHolder
    {
        public IServiceProvider ServiceProvider { get; set; }
        public IServiceProvider MessageScopeAccessorServiceProvider { get; set; }
    }

}

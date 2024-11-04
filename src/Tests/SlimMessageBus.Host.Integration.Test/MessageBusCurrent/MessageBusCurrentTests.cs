namespace SlimMessageBus.Host.Integration.Test.MessageBusCurrent;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

/// <summary>
/// This test verifies that the MessageBus.Current accessor works correctly and looks up in the current message scope.
/// </summary>
/// <param name="output"></param>
[Trait("Category", "Integration")]
public class MessageBusCurrentTests(ITestOutputHelper output) : BaseIntegrationTest<MessageBusCurrentTests>(output)
{
    protected override void SetupServices(ServiceCollection services, IConfigurationRoot configuration)
    {
        services.AddSlimMessageBus(mbb =>
        {
            mbb.AddChildBus("Memory", builder =>
            {
                builder
                    .WithProviderMemory()
                    .AutoDeclareFrom(Assembly.GetExecutingAssembly(), t => t.Namespace.Contains("MessageBusCurrent"))
                    .PerMessageScopeEnabled(false);
            });
            mbb.AddServicesFromAssemblyContaining<ValueChangedEventHandler>();
        });
        services.AddScoped<ValueHolder>();
    }

    [Fact]
    public async Task Given_MemoryConsumer_When_MessageBusCurrentCalledInsideConsumer_Then_LooksUpInTheMessageScope()
    {
        // Arrange
        using var scope = ServiceProvider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var value = Guid.NewGuid();

        // Act
        await bus.Publish(new SetValueCommand(value));

        // Assert
        var valueHolder = scope.ServiceProvider.GetRequiredService<ValueHolder>();
        valueHolder.Value.Should().Be(value);
    }

    public record SetValueCommand(Guid Value);

    public class SetValueCommandHandler : IRequestHandler<SetValueCommand>
    {
        public async Task OnHandle(SetValueCommand request, CancellationToken cancellationToken)
        {
            // Some other logic here ...

            // and then notify about the value change using the MessageBus.Current accessor which should look up in the current message scope
            await MessageBus.Current.Publish(new ValueChangedEvent(request.Value), cancellationToken: cancellationToken);
        }
    }

    public record ValueChangedEvent(Guid Value);

    public class ValueChangedEventHandler(ValueHolder valueHolder) : IRequestHandler<ValueChangedEvent>
    {
        public Task OnHandle(ValueChangedEvent request, CancellationToken cancellationToken)
        {
            valueHolder.Value = request.Value;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Holds the value (per scope lifetime).
    /// </summary>
    public class ValueHolder
    {
        public Guid Value { get; set; }
    }
}

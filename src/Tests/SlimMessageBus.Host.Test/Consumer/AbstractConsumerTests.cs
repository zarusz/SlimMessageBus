﻿namespace SlimMessageBus.Host.Test.Consumer;

public class AbstractConsumerTests
{
    private class TestConsumer(ILogger logger, IEnumerable<AbstractConsumerSettings> settings, IEnumerable<IAbstractConsumerInterceptor> interceptors)
        : AbstractConsumer(logger, settings, path: "path", interceptors)
    {
        internal protected override Task OnStart() => Task.CompletedTask;
        internal protected override Task OnStop() => Task.CompletedTask;
    }

    private class TestConsumerSettings : AbstractConsumerSettings;

    private readonly List<AbstractConsumerSettings> _settings;
    private readonly Mock<AbstractConsumer> _targetMock;
    private readonly AbstractConsumer _target;
    private readonly Mock<IAbstractConsumerInterceptor> _interceptor;

    public AbstractConsumerTests()
    {
        _interceptor = new Mock<IAbstractConsumerInterceptor>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton(_interceptor.Object));

        var testSettings = new TestConsumerSettings
        {
            MessageBusSettings = new MessageBusSettings { ServiceProvider = serviceCollection.BuildServiceProvider() }
        };

        _settings = [testSettings];

        _targetMock = new Mock<AbstractConsumer>(NullLogger.Instance, _settings, "path", new IAbstractConsumerInterceptor[] { _interceptor.Object }) { CallBase = true };
        _target = _targetMock.Object;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_Start_Then_Interceptor_CanStartIsCalled(bool canStart)
    {
        // Arrange
        _interceptor.Setup(x => x.CanStart(_target)).ReturnsAsync(canStart);

        // Act
        await _target.Start();

        // Assert
        _target.IsStarted.Should().BeTrue();

        _interceptor.Verify(x => x.CanStart(_target), Times.Once);
        _interceptor.VerifyGet(x => x.Order, Times.Once);
        _interceptor.VerifyNoOtherCalls();

        _targetMock.Verify(x => x.OnStart(), canStart ? Times.Once : Times.Never);
        _targetMock.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_Stop_Then_Interceptor_CanStopIsCalled(bool canStop)
    {
        // Arrange
        _interceptor.Setup(x => x.CanStart(_target)).ReturnsAsync(true);
        _interceptor.Setup(x => x.CanStop(_target)).ReturnsAsync(canStop);

        await _target.Start();

        // Act
        await _target.Stop();

        // Assert
        _target.IsStarted.Should().BeFalse();

        _interceptor.Verify(x => x.CanStart(_target), Times.Once);
        _interceptor.Verify(x => x.CanStop(_target), Times.Once);
        _interceptor.VerifyGet(x => x.Order, Times.Once);
        _interceptor.VerifyNoOtherCalls();

        _targetMock.Verify(x => x.OnStart(), Times.Once);
        _targetMock.Verify(x => x.OnStop(), canStop ? Times.Once : Times.Never);
        _targetMock.VerifyNoOtherCalls();
    }
}
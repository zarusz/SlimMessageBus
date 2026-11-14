namespace SlimMessageBus.Host.Memory.Test;

using Microsoft.Extensions.DependencyInjection;

using SlimMessageBus.Host.Consumer;
using SlimMessageBus.Host.Interceptor;

public class MemoryConsumerContextTests
{
    [Fact]
    public async Task ConcurrentSendsInOneScopeUseSeparateContextsAndShareScopedDependencies()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeLimit = TimeSpan.FromSeconds(10);

        await using var provider = CreateProvider(services => services
            .AddSingleton<IConsumerInterceptor<FirstRequest>>(new InspectRequest<FirstRequest>(async (_, next, _) =>
            {
                firstEntered.SetResult();
                await release.Task.WaitAsync(timeLimit);
                return await next();
            }))
            .AddSingleton<IConsumerInterceptor<SecondRequest>>(new InspectRequest<SecondRequest>(async (_, next, _) =>
            {
                secondEntered.SetResult();
                await release.Task.WaitAsync(timeLimit);
                return await next();
            })));
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = bus.Send(new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "first" });
        await firstEntered.Task.WaitAsync(timeLimit);
        var second = bus.Send(new SecondRequest(), headers: new Dictionary<string, object> { ["marker"] = "second" });
        try
        {
            await secondEntered.Task.WaitAsync(timeLimit);
        }
        finally
        {
            release.SetResult();
        }

        var results = await Task.WhenAll(first, second);

        results.Select(x => (x.Path, x.Marker)).Should().Equal(("first", "first"), ("second", "second"));
        results.Should().OnlyContain(x => x.Dependency == scope.ServiceProvider.GetRequiredService<ScopedDependency>());
    }

    [Fact]
    public async Task NestedSendRestoresTheOuterContextForAReusedScopedHandler()
    {
        var interceptor = new InspectRequest<FirstRequest>(async (_, next, context) =>
        {
            if (Equals(context.Headers["marker"], "inner"))
            {
                await Task.Yield();
                return await next();
            }

            // Resolve the outer handler before re-entering it in the same caller DI scope.
            var outerServiceProvider = MessageScope.Current;
            var outerHandler = context.Consumer;
            var inner = await context.Bus.Send(new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "inner" });
            inner.Marker.Should().Be("inner");
            inner.Handler.Should().BeSameAs(outerHandler);

            MessageScope.Current.Should().BeSameAs(outerServiceProvider);
            context.Headers["marker"].Should().Be("outer");
            return await next();
        });
        await using var provider = CreateProvider(services => services.AddSingleton<IConsumerInterceptor<FirstRequest>>(interceptor));
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.Send(new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "outer" });

        result.Marker.Should().Be("outer");
        var injectedContext = scope.ServiceProvider.GetRequiredService<IConsumerContext>();
        var readPath = () => injectedContext.Path;
        readPath.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SendFromRootProviderWorksWithScopeValidation()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSlimMessageBus(mbb => mbb.WithProviderMemory()
                .Produce<FirstRequest>(x => x.DefaultTopic("first"))
                .Handle<FirstRequest, Reply>(x => x.Topic("first").WithHandler<RootHandler>()))
            .AddTransient<RootHandler>()
            .BuildServiceProvider(validateScopes: true);

        var result = await provider.GetRequiredService<IMessageBus>().Send(
            new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "root" });

        (result.Path, result.Marker).Should().Be(("first", "root"));
    }

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<ScopedDependency>()
            .AddScoped<Handler<FirstRequest>>()
            .AddScoped<Handler<SecondRequest>>()
            .AddSlimMessageBus(mbb => mbb.WithProviderMemory()
                .Produce<FirstRequest>(x => x.DefaultTopic("first"))
                .Produce<SecondRequest>(x => x.DefaultTopic("second"))
                .Handle<FirstRequest, Reply>(x => x.Topic("first").WithHandler<Handler<FirstRequest>>())
                .Handle<SecondRequest, Reply>(x => x.Topic("second").WithHandler<Handler<SecondRequest>>()));

        configure(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public sealed record FirstRequest : IRequest<Reply>;
    public sealed record SecondRequest : IRequest<Reply>;
    public sealed record Reply(string Path, object Marker, ScopedDependency Dependency, object Handler);
    public sealed class ScopedDependency;

    public sealed class Handler<T>(IConsumerContext context, ScopedDependency dependency) : IRequestHandler<T, Reply>
    {
        public Task<Reply> OnHandle(T request, CancellationToken cancellationToken)
            => Task.FromResult(new Reply(context.Path, context.Headers["marker"], dependency, this));
    }

    public sealed class RootHandler(IConsumerContext context) : IRequestHandler<FirstRequest, Reply>
    {
        public Task<Reply> OnHandle(FirstRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new Reply(context.Path, context.Headers["marker"], null, this));
    }

    private sealed class InspectRequest<T>(Func<T, Func<Task<object>>, IConsumerContext, Task<object>> inspect) : IConsumerInterceptor<T>
    {
        public Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context) => inspect(message, next, context);
    }
}

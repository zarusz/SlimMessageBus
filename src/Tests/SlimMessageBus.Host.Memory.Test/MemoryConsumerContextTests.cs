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

            var outerServiceProvider = MessageScope.Current;
            var outer = (Reply)await next();
            var inner = await context.Bus.Send(new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "inner" });
            inner.Marker.Should().Be("inner");
            inner.Handler.Should().BeSameAs(outer.Handler);

            MessageScope.Current.Should().BeSameAs(outerServiceProvider);
            context.Headers["marker"].Should().Be("outer");
            outer.Dependency.Marker.Should().Be("outer");
            return outer;
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
    public async Task ConcurrentSendsToOneScopedLegacyHandlerReadTheirOwnContext()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeLimit = TimeSpan.FromSeconds(10);

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddScoped<LegacyHandler>()
            .AddSlimMessageBus(mbb => mbb.WithProviderMemory()
                .Produce<LegacyRequest>(x => x.DefaultTopic("legacy"))
                .Handle<LegacyRequest, string>(x => x.Topic("legacy").WithHandler<LegacyHandler>()))
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = bus.Send(new LegacyRequest(async () =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(timeLimit);
        }), headers: new Dictionary<string, object> { ["marker"] = "first" });

        try
        {
            await firstEntered.Task.WaitAsync(timeLimit);
            var second = await bus.Send(new LegacyRequest(() => Task.CompletedTask),
                headers: new Dictionary<string, object> { ["marker"] = "second" }).WaitAsync(timeLimit);
            second.Should().Be("second");
        }
        finally
        {
            releaseFirst.SetResult();
        }

        (await first).Should().Be("first");
    }

    [Fact]
    public async Task ScopedDependencyFollowsNestedSendThatReusesTheOuterMessageScope()
    {
        Reply inner = null;
        var interceptor = new InspectRequest<FirstRequest>(async (_, next, context) =>
        {
            var outer = (Reply)await next();
            inner = await context.Bus.Send(new SecondRequest(), headers: new Dictionary<string, object> { ["marker"] = "inner" });
            outer.Dependency.Marker.Should().Be("outer");
            return outer;
        });
        await using var provider = CreateProvider(services => services.AddSingleton<IConsumerInterceptor<FirstRequest>>(interceptor), createScopeForFirstRequest: true);
        using var scope = provider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outer = await bus.Send(new FirstRequest(), headers: new Dictionary<string, object> { ["marker"] = "outer" });

        (outer.Path, outer.Marker).Should().Be(("first", "outer"));
        (inner.Path, inner.Marker).Should().Be(("second", "inner"));
        inner.Dependency.Should().BeSameAs(outer.Dependency);
        outer.Dependency.Should().NotBeSameAs(scope.ServiceProvider.GetRequiredService<ScopedDependency>());
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

    private static ServiceProvider CreateProvider(Action<IServiceCollection> configure, bool createScopeForFirstRequest = false)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<ScopedDependency>()
            .AddScoped<Handler<FirstRequest>>()
            .AddScoped<Handler<SecondRequest>>()
            .AddSlimMessageBus(mbb => mbb.WithProviderMemory()
                .Produce<FirstRequest>(x => x.DefaultTopic("first"))
                .Produce<SecondRequest>(x => x.DefaultTopic("second"))
                .Handle<FirstRequest, Reply>(x => x.Topic("first").WithHandler<Handler<FirstRequest>>()
                    .Do(b => b.ConsumerSettings.IsMessageScopeEnabled = createScopeForFirstRequest))
                .Handle<SecondRequest, Reply>(x => x.Topic("second").WithHandler<Handler<SecondRequest>>()));

        configure(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public sealed record FirstRequest : IRequest<Reply>;
    public sealed record SecondRequest : IRequest<Reply>;
    public sealed record LegacyRequest(Func<Task> BeforeReadingContext) : IRequest<string>;
    public sealed record Reply(string Path, object Marker, ScopedDependency Dependency, object Handler);

    public sealed class ScopedDependency(IConsumerContext context)
    {
        public object Marker => context.Headers["marker"];
    }

    public sealed class Handler<T>(IConsumerContext context, ScopedDependency dependency) : IRequestHandler<T, Reply>
    {
        public Task<Reply> OnHandle(T request, CancellationToken cancellationToken)
            => Task.FromResult(new Reply(context.Path, dependency.Marker, dependency, this));
    }

    public sealed class LegacyHandler : IRequestHandler<LegacyRequest, string>, IConsumerWithContext
    {
        public IConsumerContext Context { get; set; }

        public async Task<string> OnHandle(LegacyRequest request, CancellationToken cancellationToken)
        {
            await request.BeforeReadingContext();
            return (string)Context.Headers["marker"];
        }
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

namespace SlimMessageBus.Host.AspNetCore.Test;

using Sample.DomainEvents.Domain;
using Sample.DomainEvents.WebApi;

using SlimMessageBus.Host.Interceptor;

/// <summary>
/// This integration test checks that the MessageBus.Current lookup in the API method is scoped to the ongoing HTTP request.
/// Its based on the DomainEvents AspNet Core sample.
/// </summary>
public class AspNetCoreIt : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly Mock<IConsumerInterceptor<OrderSubmittedEvent>> _orderSubmittedConsumerInterceptor = new();

    public AspNetCoreIt(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _factory.OnConfigureServices = services =>
        {
            services.AddScoped(_ => _orderSubmittedConsumerInterceptor.Object);
            services.AddSingleton<RequestIdCollector>();
            services.AddTransient(typeof(IConsumerInterceptor<>), typeof(InterceptorThatStoresRequestId<>));
        };

        _orderSubmittedConsumerInterceptor
            .Setup(x => x.OnHandle(It.IsAny<OrderSubmittedEvent>(), It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()))
            .Returns<OrderSubmittedEvent, Func<Task<object>>, IConsumerContext>(async (e, next, context) =>
            {
                return await next();
            });
    }

    [Fact]
    public async Task Given_AspNetCoreApp_When_SwaggerUI_Then_Responds()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString().Should().Be("text/html; charset=utf-8");
    }

    [Fact]
    public async Task Given_AspNetCoreApiMethodWhichUsesCurrentMessageBusLookup_When_CurrentMessageBusLookedInApiMethodCalled_Then_ItLooksUpTheBusInTheOngoingHttpRequestScope()
    {
        // Arrange
        var client = _factory.CreateClient();

        var requestId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Orders")
        {
            Content = new StringContent("{}", new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")),
            Headers =
            {
                { "X-Request-Id", requestId }
            }
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        _orderSubmittedConsumerInterceptor
            .Verify(
                x => x.OnHandle(It.IsAny<OrderSubmittedEvent>(), It.IsAny<Func<Task<object>>>(), It.IsAny<IConsumerContext>()),
                Times.Exactly(2)); // there are 2 domain event consumers in the sample

        var interceptor = _factory.Services.GetRequiredService<RequestIdCollector>();
        var uniqueRequestIds = interceptor.RequestIds.Distinct().ToList();
        uniqueRequestIds.Should().HaveCount(1);
        uniqueRequestIds.Should().Contain(requestId);
    }
}

public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    public Action<IServiceCollection> OnConfigureServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            OnConfigureServices?.Invoke(services);
        });
    }
}

public class RequestIdCollector
{
    public List<string> RequestIds { get; } = [];
}

public class InterceptorThatStoresRequestId<T>(IHttpContextAccessor httpContext, RequestIdCollector requestIdCollector) : IConsumerInterceptor<T>
{
    public async Task<object> OnHandle(T message, Func<Task<object>> next, IConsumerContext context)
    {
        httpContext.HttpContext.Request.Headers.TryGetValue("X-Request-Id", out var requestId);
        requestIdCollector.RequestIds.Add(requestId);

        return await next();
    }
}
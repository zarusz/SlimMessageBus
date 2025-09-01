# RabbitMQ Health Check

The RabbitMQ health check provides a way to monitor the health of your RabbitMQ message bus connection and channel.

## Features

- Checks if the RabbitMQ channel is available and open
- Provides detailed diagnostics information
- Integrates with ASP.NET Core Health Checks
- Returns appropriate health status based on connection state

## Usage

### 1. Basic Registration

```csharp
services.AddHealthChecks()
    .AddRabbitMq();

// Register the health check service
services.AddRabbitMqHealthCheck();
```

### 2. With Custom Configuration

```csharp
services.AddHealthChecks()
    .AddRabbitMq(
        name: "rabbitmq",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "messaging", "rabbitmq" },
        timeout: TimeSpan.FromSeconds(10));
```

### 3. With Specific Message Bus Instance

```csharp
services.AddHealthChecks()
    .AddRabbitMq(
        serviceProvider => serviceProvider.GetRequiredService<RabbitMqMessageBus>(),
        name: "rabbitmq-primary");
```

### 4. Complete Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure RabbitMQ message bus
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.WithProvider<RabbitMqMessageBusSettings>(settings =>
    {
        settings.ConnectionString = "amqp://localhost";
    })
    .AddHealthChecks()
    .AddRabbitMq();
});

// Register health check service
builder.Services.AddRabbitMqHealthCheck();

var app = builder.Build();

// Configure health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

## Health Check Results

### Healthy
- Channel is available and open
- Returns diagnostic data including channel number

### Unhealthy
- Channel is null (connection failed during initialization)
- Channel is closed
- Exception occurred during health check

## Diagnostic Data

The health check provides the following diagnostic information:

- `ChannelNumber`: The RabbitMQ channel number
- `ChannelIsOpen`: Whether the channel is currently open
- `CloseReason`: Reason for channel closure (when applicable)
- `Exception`: Exception message (when applicable)

## Integration with Circuit Breaker

The RabbitMQ health check can be used with the circuit breaker pattern to automatically disable consumers when the connection is unhealthy:

```csharp
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProvider<RabbitMqMessageBusSettings>(settings =>
    {
        settings.ConnectionString = "amqp://localhost";
    })
    .AddConsumer<MyMessage>(builder => builder
        .Topic("my-topic")
        .WithHealthCheckCircuitBreaker("rabbitmq") // Use health check to control consumer
    );
});
```

This ensures that consumers are automatically paused when RabbitMQ is unhealthy, preventing message loss and improving system resilience.
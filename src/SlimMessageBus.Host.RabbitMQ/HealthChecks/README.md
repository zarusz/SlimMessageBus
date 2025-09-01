# RabbitMQ Health Check

The RabbitMQ health check provides a way to monitor the health of your RabbitMQ message bus connection and channel, especially useful with the enhanced connection resilience features.

## Architecture

The RabbitMQ message bus now uses a modular architecture with clear separation of concerns:

- **`RabbitMqMessageBus`**: Main message bus implementation that handles message production and consumption
- **`RabbitMqChannelManager`**: Dedicated connection and channel management with automatic recovery (implements `IRabbitMqChannel`)
- **`RabbitMqHealthCheck`**: Health monitoring for connection status (depends on `IRabbitMqChannel`)

This separation improves maintainability, testability, and allows for better error handling and recovery mechanisms. The health check now depends on the `IRabbitMqChannel` interface instead of the concrete `RabbitMqMessageBus` class, providing better decoupling.

## Features

- Checks if the RabbitMQ channel is available and open
- Provides detailed diagnostics information
- Integrates with ASP.NET Core Health Checks
- Returns appropriate health status based on connection state
- Works with the enhanced connection retry mechanism
- Uses `IRabbitMqChannel` interface for better separation of concerns

## Connection Resilience

The `RabbitMqChannelManager` provides enhanced connection resilience:

- **Continuous Retry**: Unlike the previous 3-attempt limit, the connection now retries indefinitely in the background
- **Automatic Recovery**: Integrates with RabbitMQ's built-in automatic recovery features
- **Connection Monitoring**: Monitors connection shutdown events and triggers reconnection
- **Health Integration**: The health check provides visibility into connection retry status
- **Thread-Safe Operations**: All channel operations are properly synchronized

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

### 3. With Specific IRabbitMqChannel Instance

```csharp
services.AddHealthChecks()
    .AddRabbitMq(
        serviceProvider => serviceProvider.GetRequiredService<IRabbitMqChannel>(),
        name: "rabbitmq-primary");
```

### 4. Complete Example with Enhanced Resilience

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure RabbitMQ message bus with enhanced resilience
builder.Services.AddSlimMessageBus(mbb =>
{
    mbb.WithProvider<RabbitMqMessageBusSettings>(settings =>
    {
        settings.ConnectionString = "amqp://localhost";
        // The connection factory's NetworkRecoveryInterval will be used for retry timing
        settings.ConnectionFactory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
        settings.ConnectionFactory.AutomaticRecoveryEnabled = true; // This is set automatically
    })
    .AddHealthChecks()
    .AddRabbitMq();
});

// Register health check service - this automatically resolves the IRabbitMqChannel
// from the registered RabbitMqMessageBus (which implements IRabbitMqChannel)
builder.Services.AddRabbitMqHealthCheck();

var app = builder.Build();

// Configure health check endpoint
app.MapHealthChecks("/health");

app.Run();
```

## Health Check Results

### Healthy
- Channel is available and open
- Returns diagnostic data including channel number and connection status

### Unhealthy
- Channel is null (connection failed and retry is in progress)
- Channel is closed (connection retry may be in progress)
- Exception occurred during health check

## Diagnostic Data

The health check provides the following diagnostic information:

- `ChannelAvailable`: Whether the channel object exists
- `ChannelNumber`: The RabbitMQ channel number (when available)
- `ChannelIsOpen`: Whether the channel is currently open
- `ConnectionStatus`: Overall connection status description
- `CloseReason`: Reason for channel closure (when applicable)
- `Exception`: Exception message (when applicable)

## Enhanced Resilience Features

### RabbitMqChannelManager

The new `RabbitMqChannelManager` class provides:

#### **Continuous Connection Retry**
- **No Retry Limit**: Unlike the previous 3-attempt limit during startup, connection retries now continue indefinitely
- **Background Retry**: Failed connections trigger a background timer that continuously attempts reconnection
- **Configurable Interval**: Retry interval uses the `NetworkRecoveryInterval` from the connection factory (default 10 seconds)

#### **Automatic Recovery Integration**
- **Built-in Recovery**: Leverages RabbitMQ client's automatic recovery features
- **Event Monitoring**: Monitors connection shutdown events to trigger custom retry logic
- **Graceful Handling**: Distinguishes between expected shutdowns (during disposal) and unexpected failures

#### **Thread-Safe Channel Operations**
- **ExecuteWithChannel**: Provides safe access to channel operations with automatic locking
- **EnsureChannel**: Validates channel availability and triggers reconnection if needed
- **Proper Synchronization**: All channel access is synchronized to prevent race conditions

#### **Improved Error Handling**
- **Immediate Retry Trigger**: When `EnsureChannel()` detects a null channel, it triggers immediate reconnection
- **Better Logging**: Enhanced logging provides clear visibility into connection state and retry attempts
- **Health Check Integration**: Health checks provide real-time visibility into connection status

### Example Usage in Message Bus

The message bus now uses the channel manager for all operations:

```csharp
// Safe channel execution with automatic retry
_channelManager.ExecuteWithChannel(channel =>
{
    var batch = channel.CreateBasicPublishBatch();
    // ... batch operations
    batch.Publish();
});

// Function execution with return value
var properties = _channelManager.ExecuteWithChannel(channel => 
    channel.CreateBasicProperties());
```

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

## Configuration Options

### Retry Timing
The retry interval is controlled by the RabbitMQ connection factory's `NetworkRecoveryInterval`:

```csharp
settings.ConnectionFactory.NetworkRecoveryInterval = TimeSpan.FromSeconds(30); // 30-second retry interval
```

### Monitoring and Logging
Enhanced logging provides visibility into:
- Initial connection attempts and failures
- Background retry attempts
- Connection recovery events
- Health check status changes

Example log messages:
```
[INFO] RabbitMQ connection established successfully
[WARN] RabbitMQ connection shutdown detected. Reason: Connection forced, Initiator: Library
[INFO] Starting RabbitMQ connection retry timer with interval: 00:00:10
[INFO] Attempting to reconnect to RabbitMQ...
[INFO] RabbitMQ reconnection successful
```

## Testing

The modular architecture improves testability:

- **RabbitMqChannelManager**: Can be tested independently with mocked connections
- **RabbitMqMessageBus**: Simpler to test with the channel management logic extracted
- **RabbitMqHealthCheck**: Now uses `IRabbitMqChannel` interface, making it easier to mock and test

Example unit test for health check:
```csharp
[Fact]
public async Task CheckHealthAsync_Should_Return_Healthy_When_Channel_Is_Open()
{
    // Arrange
    var channelMock = new Mock<IModel>();
    channelMock.SetupGet(x => x.IsOpen).Returns(true);
    channelMock.SetupGet(x => x.ChannelNumber).Returns(456);

    var rabbitMqChannelMock = new Mock<IRabbitMqChannel>();
    rabbitMqChannelMock.Setup(x => x.Channel).Returns(channelMock.Object);
    
    var healthCheck = new RabbitMqHealthCheck(rabbitMqChannelMock.Object, loggerMock.Object);

    // Act
    var result = await healthCheck.CheckHealthAsync(context);

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
}
```

## Interface-Based Design

The health check now depends on `IRabbitMqChannel` instead of the concrete `RabbitMqMessageBus`:

- **Better Decoupling**: Health check doesn't need to know about message bus internals
- **Easier Testing**: Can mock `IRabbitMqChannel` interface instead of complex message bus
- **Flexible Implementation**: Any class implementing `IRabbitMqChannel` can be health-checked
- **Single Responsibility**: Health check focuses solely on channel health, not message bus operations
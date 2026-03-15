global using System.Net.Mime;
global using System.Reflection;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

global using Sample.CircuitBreaker.HealthCheck.Consumers;
global using Sample.CircuitBreaker.HealthCheck.Models;

global using SecretStore;

global using SlimMessageBus;
global using SlimMessageBus.Host;
global using SlimMessageBus.Host.RabbitMQ;
global using SlimMessageBus.Host.Serialization.SystemTextJson;

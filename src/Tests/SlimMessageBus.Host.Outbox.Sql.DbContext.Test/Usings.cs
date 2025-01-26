global using System.Diagnostics;
global using System.Reflection;

global using Confluent.Kafka;

global using FluentAssertions;

global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using SecretStore;

global using SlimMessageBus.Host.AzureServiceBus;
global using SlimMessageBus.Host.Kafka;
global using SlimMessageBus.Host.Memory;
global using SlimMessageBus.Host.Outbox.Sql.DbContext.Test.DataAccess;
global using SlimMessageBus.Host.Serialization.SystemTextJson;
global using SlimMessageBus.Host.Test.Common.IntegrationTest;

global using Xunit;
global using Xunit.Abstractions;
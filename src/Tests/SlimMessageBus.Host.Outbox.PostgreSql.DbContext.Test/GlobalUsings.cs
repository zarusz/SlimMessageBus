global using System.Diagnostics;
global using System.Net.Mime;
global using System.Reflection;

global using Confluent.Kafka;
global using Confluent.Kafka.Admin;

global using FluentAssertions;

global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Migrations;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using SecretStore;

global using SlimMessageBus.Host.AzureServiceBus;
global using SlimMessageBus.Host.Kafka;
global using SlimMessageBus.Host.Memory;
global using SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.DataAccess;
global using SlimMessageBus.Host.Outbox.PostgreSql.DbContext.Test.Fixtures;
global using SlimMessageBus.Host.Outbox.PostgreSql.Transactions;
global using SlimMessageBus.Host.Outbox.Services;
global using SlimMessageBus.Host.RabbitMQ;
global using SlimMessageBus.Host.Serialization.SystemTextJson;
global using SlimMessageBus.Host.Test.Common.IntegrationTest;

global using Testcontainers.Kafka;
global using Testcontainers.PostgreSql;
global using Testcontainers.RabbitMq;

global using Xunit;
global using Xunit.Abstractions;

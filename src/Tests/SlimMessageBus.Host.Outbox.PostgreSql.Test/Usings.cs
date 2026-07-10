global using System.Diagnostics;
global using System.Text.Json;
global using System.Threading.Tasks;

global using AutoFixture;

global using AwesomeAssertions;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging.Abstractions;
global using Microsoft.Extensions.Time.Testing;

global using Moq;

global using Npgsql;

global using SlimMessageBus.Host.Outbox.Services;
global using SlimMessageBus.Host.Outbox.PostgreSql.Configuration;
global using SlimMessageBus.Host.Outbox.PostgreSql.Repositories;
global using SlimMessageBus.Host.Outbox.PostgreSql.Services;
global using SlimMessageBus.Host.Outbox.PostgreSql.Transactions;
global using SlimMessageBus.Host.Serialization;

global using Xunit;

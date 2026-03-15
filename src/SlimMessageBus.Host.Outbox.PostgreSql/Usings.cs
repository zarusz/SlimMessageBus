global using System;
global using System.Collections.Generic;
global using System.Data;
global using System.Diagnostics.CodeAnalysis;
global using System.Reflection;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading;
global using System.Threading.Tasks;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;

global using Npgsql;

global using NpgsqlTypes;

global using SlimMessageBus.Host.Interceptor;
global using SlimMessageBus.Host.Outbox.PostgreSql.Configuration;
global using SlimMessageBus.Host.Outbox.PostgreSql.Repositories;
global using SlimMessageBus.Host.Outbox.PostgreSql.Services;
global using SlimMessageBus.Host.Outbox.PostgreSql.Transactions;
global using SlimMessageBus.Host.PostgreSql;

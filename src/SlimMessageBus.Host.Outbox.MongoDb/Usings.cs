global using System;
global using System.Collections.Generic;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading;
global using System.Threading.Tasks;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;

global using MongoDB.Bson;
global using MongoDB.Bson.Serialization.Attributes;
global using MongoDB.Driver;

global using SlimMessageBus.Host.Interceptor;
global using SlimMessageBus.Host.Outbox.MongoDb.Configuration;
global using SlimMessageBus.Host.Outbox.MongoDb.Interceptors;
global using SlimMessageBus.Host.Outbox.MongoDb.Repositories;
global using SlimMessageBus.Host.Outbox.MongoDb.Services;
global using SlimMessageBus.Host.Outbox.MongoDb.Transactions;

using System;
using System.Globalization;
using System.Linq;
using Common.Logging;
using Common.Logging.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Sample.DomainEvents.Domain;
using SlimMessageBus;
using SlimMessageBus.Host.AspNetCore;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.Json;

namespace Sample.DomainEvents.WebApi
{
    public class Startup
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Startup));

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // Setup Common.Logging
            var logConfiguration = new LogConfiguration();
            configuration.GetSection("LogConfiguration").Bind(logConfiguration);
            LogManager.Configure(logConfiguration);
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            ConfigureMessageBus(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();

            ConfigureMessageBus(app);
        }

        public void ConfigureMessageBus(IApplicationBuilder app)
        {
            // Set the MessageBus provider, so that IMessageBus are resolved from the current request scope
            MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().From(app).Build());
        }

        public void ConfigureMessageBus(IServiceCollection services)
        {
            services.AddScoped<OrderSubmittedHandler>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>(); // This is required for the SlimMessageBus.Host.AspNetCore plugin

            // Make the MessageBus per request scope
            services.AddScoped<IMessageBus>(BuildMessageBus);
        }

        private IMessageBus BuildMessageBus(IServiceProvider serviceProvider)
        {
            var domainAssembly = typeof(OrderSubmittedEvent).Assembly;

            var mbb = MessageBusBuilder.Create()
                // declare that OrderSubmittedEvent will be produced
                .Produce<OrderSubmittedEvent>(x => x.DefaultTopic(x.Settings.MessageType.Name))
                // declare that OrderSubmittedEvent will be consumed by OrderSubmittedHandler
                //.SubscribeTo<OrderSubmittedEvent>(x => x.Topic(x.MessageType.Name).WithSubscriber<OrderSubmittedHandler>())
                // Note: we could discover messages and handlers using reflection and register them automatically
                .Do(builder => domainAssembly
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .SelectMany(t => t.GetInterfaces(), (t, i) => new { Type = t, Interface = i })
                    .Where(x => x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == typeof(IConsumer<>))
                    .Select(x => new { HandlerType = x.Type, EventType = x.Interface.GetGenericArguments()[0] })
                    .ToList()
                    .ForEach(find =>
                    {
                        Log.InfoFormat(CultureInfo.InvariantCulture, "Registering {0} in the bus", find.EventType);
                        builder.SubscribeTo(find.EventType, x => x.Topic(x.MessageType.Name).WithSubscriber(find.HandlerType));
                    })
                )
                .WithSerializer(new JsonMessageSerializer()) // Use JSON for message serialization                
                .WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider))
                .WithProviderMemory(new MemoryMessageBusSettings
                {
                    // Don't serialize the domain events and rather pass the same instance across handlers
                    EnableMessageSerialization = false
                });

            return mbb.Build();
        }
    }
}

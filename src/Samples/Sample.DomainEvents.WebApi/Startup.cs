using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host.AspNetCore;
using SlimMessageBus.Host.Config;

namespace Sample.DomainEvents.WebApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Make the MessageBus per request scope
            services.AddScoped<IMessageBus>(sp => new DummyMessageBus());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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

            // Set the MessageBus provider to be resolved from the request scope 
            MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().FromPerRequestScope(app).Build());
        }

        public class DummyMessageBus : IMessageBus
        {
            public string Id { get; }

            public DummyMessageBus()
            {
                Id = Guid.NewGuid().ToString();
            }

#pragma warning disable CA1063 // Implement IDisposable Correctly
            public void Dispose()
#pragma warning restore CA1063 // Implement IDisposable Correctly
            {
            }

            public Task Publish<TMessage>(TMessage message, string topic = null)
            {
                throw new NotImplementedException();
            }

            public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }

            public Task<TResponseMessage> Send<TResponseMessage>(IRequestMessage<TResponseMessage> request, TimeSpan timeout, string topic = null, CancellationToken cancellationToken = default(CancellationToken))
            {
                throw new NotImplementedException();
            }
        }
    }
}

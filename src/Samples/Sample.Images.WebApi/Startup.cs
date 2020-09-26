using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using SlimMessageBus;
using SlimMessageBus.Host.AspNetCore;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.Kafka.Configs;
using SlimMessageBus.Host.Serialization.Json;

namespace Sample.Images.WebApi
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
            services.AddLogging(cfg =>
            {
                cfg.AddConfiguration(Configuration);
                cfg.AddConsole();
            });

            services.AddControllers();

            // register services
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), @"..\Content");
            services.AddSingleton<IFileStore>(x => new DiskFileStore(imagesPath));
            services.AddSingleton<IThumbnailFileIdStrategy, SimpleThumbnailFileIdStrategy>();

            // register MessageBus  
            ConfigureMessageBus(services);
        }

        private void ConfigureMessageBus(IServiceCollection services)
        {
            services.AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin

            services.AddSingleton<IMessageBus>(BuildMessageBus);
            
            services.AddSingleton<IRequestResponseBus>(svp => svp.GetService<IMessageBus>());

            // register any consumers (IConsumer<>) or handlers (IHandler<>) - if any
        }

        private IMessageBus BuildMessageBus(IServiceProvider serviceProvider)
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = Configuration["InstanceId"];
            var kafkaBrokers = Configuration["Kafka:Brokers"];

            var instanceGroup = $"webapi-{instanceId}";
            var instanceReplyTo = $"webapi-{instanceId}-response";

            var messageBusBuilder = MessageBusBuilder.Create()
                .Produce<GenerateThumbnailRequest>(x =>
                {
                    // Default response timeout for this request type
                    //x.DefaultTimeout(TimeSpan.FromSeconds(10));
                    x.DefaultTopic("thumbnail-generation");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic(instanceReplyTo);
                    x.Group(instanceGroup);
                    // Default global response timeout
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                //.WithDependencyResolverAsServiceLocator()
                //.WithDependencyResolverAsAutofac()
                .WithDependencyResolver(new AspNetCoreMessageBusDependencyResolver(serviceProvider))
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            //app.UseCors();

            //app.UseAuthentication();
            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });

            // Force the singleton SMB instance to be created on app start rather than when requested.
            // We want message to be consumed when right away when WebApi starts (more info https://stackoverflow.com/a/39006021/1906057)
            var messageBus = app.ApplicationServices.GetRequiredService<IMessageBus>();
        }
    }
}

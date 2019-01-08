using System;
using System.IO;
using Common.Logging;
using Common.Logging.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            var logConfiguration = new LogConfiguration();
            configuration.GetSection("LogConfiguration").Bind(logConfiguration);
            LogManager.Configure(logConfiguration);
        }

        public IConfiguration Configuration { get; }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // register services
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), @"..\Content");
            services.AddSingleton<IFileStore>(x => new DiskFileStore(imagesPath));
            services.AddSingleton<IThumbnailFileIdStrategy, SimpleThumbnailFileIdStrategy>();

            // register MessageBus  
            services.AddSingleton<IMessageBus>(svp => BuildMessageBus(svp.GetRequiredService<IHttpContextAccessor>()));
            services.AddSingleton<IRequestResponseBus>(svp => svp.GetService<IMessageBus>());

            services.AddMvc();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        private IMessageBus BuildMessageBus(IHttpContextAccessor httpContextAccessor)
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
                .WithDependencyResolverAsAspNetCore(httpContextAccessor)
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));

            var messageBus = messageBusBuilder.Build();
            return messageBus;
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

            // Force the singleton SMB instance to be created on app start rather than when requested.
            // We want message to be consumed when right away when WebApi starts (more info https://stackoverflow.com/a/39006021/1906057)
            var messageBus = app.ApplicationServices.GetService<IMessageBus>();
        }
    }
}

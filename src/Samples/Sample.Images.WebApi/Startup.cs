using Autofac;
using Common.Logging;
using Common.Logging.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sample.Images.FileStore;
using Sample.Images.FileStore.Disk;
using Sample.Images.Messages;
using SlimMessageBus;
using SlimMessageBus.Host.Autofac;
using SlimMessageBus.Host.Config;
using SlimMessageBus.Host.Redis;
using SlimMessageBus.Host.Serialization.Json;
using SlimMessageBus.Host.ServiceLocator;
using System;
using System.IO;

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

        public IContainer ApplicationContainer
        {
            get => _container;
            set
            {
                _container = value;
                AutofacMessageBusDependencyResolver.Container = value;
            }
        }
        private IContainer _container;

        // This method gets called by the runtime. Use this method to add services to the container.
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you. If you
        // need a reference to the container, you need to use the
        // "Without ConfigureContainer" mechanism shown later.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "..\\Content");
            builder.Register(x => new DiskFileStore(imagesPath)).As<IFileStore>().SingleInstance();
            builder.RegisterType<SimpleThumbnailFileIdStrategy>().As<IThumbnailFileIdStrategy>().SingleInstance();

            // SlimMessageBus
            var messageBus = BuildMessageBus();
            builder.RegisterInstance(messageBus).AsImplementedInterfaces();
        }

        private IMessageBus BuildMessageBus()
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = Configuration["InstanceId"];
            var kafkaBrokers = Configuration["Kafka:Brokers"];

            // configuration settings for Redis
            var redisServer = Configuration["Redis:Server"];
            var redisSyncTimeout = 0;
            redisSyncTimeout = int.TryParse(Configuration["Redis:SyncTimeout"], out redisSyncTimeout) ? redisSyncTimeout : 5000;

            var instanceGroup = $"webapi-{instanceId}";
            var instanceReplyTo = $"webapi-{instanceId}-response";

            var messageBusBuilder = new MessageBusBuilder()
                .Publish<GenerateThumbnailRequest>(x =>
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
                .WithDependencyResolverAsAutofac()
                .WithSerializer(new JsonMessageSerializer())
                //.WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers))
                .WithProviderRedis(new RedisMessageBusSettings(redisServer, redisSyncTimeout));

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
        }
    }
}

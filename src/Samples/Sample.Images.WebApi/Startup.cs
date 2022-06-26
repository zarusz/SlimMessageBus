namespace Sample.Images.WebApi;

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
using SlimMessageBus.Host.Kafka;
using SlimMessageBus.Host.MsDependencyInjection;
using SlimMessageBus.Host.Serialization.Json;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration) => Configuration = configuration;        

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

        services.AddSlimMessageBus((mbb, svp) =>
        {
            // unique id across instances of this application (e.g. 1, 2, 3)
            var instanceId = Configuration["InstanceId"];
            var kafkaBrokers = Configuration["Kafka:Brokers"];

            var instanceGroup = $"webapi-{instanceId}";
            var instanceReplyTo = $"webapi-{instanceId}-response";

            mbb
                .Produce<GenerateThumbnailRequest>(x =>
                {
                    // Default response timeout for this request type
                    //x.DefaultTimeout(TimeSpan.FromSeconds(10));
                    x.DefaultTopic("thumbnail-generation");
                })
                .ExpectRequestResponses(x =>
                {
                    x.ReplyToTopic(instanceReplyTo);
                    x.KafkaGroup(instanceGroup);
                    // Default global response timeout
                    x.DefaultTimeout(TimeSpan.FromSeconds(30));
                })
                .WithSerializer(new JsonMessageSerializer())
                .WithProviderKafka(new KafkaMessageBusSettings(kafkaBrokers));
        });
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

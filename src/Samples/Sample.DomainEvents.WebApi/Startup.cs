namespace Sample.DomainEvents.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sample.DomainEvents.Application;
using SlimMessageBus;
using SlimMessageBus.Host.DependencyResolver;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.AspNetCore;

public class Startup
{
    public Startup(IConfiguration configuration) => Configuration = configuration;

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

        ConfigureMessageBus(services);
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

        // Set the MessageBus provider, so that IMessageBus are resolved from the current request scope
        MessageBus.SetProvider(MessageBusCurrentProviderBuilder.Create().From(app).Build());
    }

    public void ConfigureMessageBus(IServiceCollection services)
    {
        services.AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin

        // Make the MessageBus per request scope
        services.AddSlimMessageBus((mbb, svp) =>
        {
            mbb
                .WithProviderMemory()
                .AutoDeclareFrom(typeof(OrderSubmittedHandler).Assembly);
        },
        addConsumersFromAssembly: new[] { typeof(OrderSubmittedHandler).Assembly });
    }
}

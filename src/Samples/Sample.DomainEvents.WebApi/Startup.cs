namespace Sample.DomainEvents.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Sample.DomainEvents.Application;
using Sample.DomainEvents.Application.DomainEventHandlers;

using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

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

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddScoped<IAuditService, AuditService>();

        services.AddSlimMessageBus(mbb =>
        {
            mbb.WithProviderMemory()
               .AutoDeclareFromAssemblyContaining<OrderSubmittedHandler>()
               .AddAspNet();
        });
        services.AddHttpContextAccessor(); // This is required for the SlimMessageBus.Host.AspNetCore plugin
    }

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

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
        });
    }
}

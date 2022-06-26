namespace Sample.Images.WebApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

public static class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
         .ConfigureWebHostDefaults(webBuilder =>
         {
             webBuilder.ConfigureKestrel(serverOptions =>
             {
                 // Set properties and call methods on options
             })
             .UseStartup<Startup>();
         });
}

namespace Sample.Images.WebApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

public static class Program
{
    public static Task Main(string[] args) =>
        Host.CreateDefaultBuilder(args)
         .ConfigureWebHostDefaults(webBuilder =>
         {
             webBuilder.UseStartup<Startup>();
         })
         .Build()
         .RunAsync();
}

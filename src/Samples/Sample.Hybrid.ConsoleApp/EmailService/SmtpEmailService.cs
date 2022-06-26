namespace Sample.Hybrid.ConsoleApp.EmailService;

using Sample.Hybrid.ConsoleApp.EmailService.Contract;
using SlimMessageBus;
using System;
using System.Threading.Tasks;

public class SmtpEmailService : IConsumer<SendEmailCommand>
{
    public Task OnHandle(SendEmailCommand message, string path)
    {
        // Sending email via SMTP...
        Console.WriteLine("--------------------------------------------");
        Console.WriteLine("- Title: {0}", message.Title);
        Console.WriteLine("- To   : {0}", message.Recipient);
        Console.WriteLine("--------------------------------------------");
        Console.WriteLine("- {0}", message.Body);
        Console.WriteLine("--------------------------------------------");

        return Task.CompletedTask;
    }
}

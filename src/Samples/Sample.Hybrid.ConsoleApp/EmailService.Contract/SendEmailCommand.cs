namespace Sample.Hybrid.ConsoleApp.EmailService.Contract;

public class SendEmailCommand
{
    public string Recipient { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
}
namespace Sample.Hybrid.ConsoleApp.EmailService.Contract;

public record SendEmailCommand
{
    public string Recipient { get; init; }
    public string Title { get; init; }
    public string Body { get; init; }
}
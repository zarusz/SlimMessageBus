namespace SlimMessageBus.Host.Test.Common;

public class XunitLoggerFactory(ITestOutputHelper output) : ILoggerFactory
{
    private readonly ITestOutputHelper _output = output;

    public ITestOutputHelper Output => _output;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void Dispose()
    {
    }
}

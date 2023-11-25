namespace SlimMessageBus.Host.Test.Common;

public class XunitLoggerFactory : ILoggerFactory
{
    private readonly ITestOutputHelper _output;

    public ITestOutputHelper Output => _output;

    public XunitLoggerFactory(ITestOutputHelper output) => _output = output;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void Dispose()
    {
    }
}

namespace SlimMessageBus.Host.Test.Common;

public class XunitLogger : Microsoft.Extensions.Logging.ILogger, IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly string categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        this.output = output;
        this.categoryName = categoryName;
    }

    public XunitLogger(ILoggerFactory loggerFactory) : this(((XunitLoggerFactory)loggerFactory).Output, string.Empty)
    {
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter)
    {
        output.WriteLine("{0}|{1}|{2}", logLevel.ToString()[..3], categoryName, state?.ToString());
        if (exception != null)
        {
            output.WriteLine("Exception: {0}", exception);
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => this;

    public void Dispose()
    {
    }
}

public class XunitLogger<T>(ILoggerFactory loggerFactory)
    : XunitLogger(((XunitLoggerFactory)loggerFactory).Output, typeof(T).Name), ILogger<T>
{
}
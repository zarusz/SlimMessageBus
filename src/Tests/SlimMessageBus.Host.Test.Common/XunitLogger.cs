namespace SlimMessageBus.Host.Test.Common;

using System.Diagnostics.CodeAnalysis;

public class XunitLogger : ILogger, IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly string categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        this.output = output;
        this.categoryName = categoryName;
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

public class XunitLogger<T> : XunitLogger, ILogger<T>
{
    public XunitLogger(ILoggerFactory loggerFactory) : base(((XunitLoggerFactory)loggerFactory).Output, typeof(T).Name)
    {
    }
}
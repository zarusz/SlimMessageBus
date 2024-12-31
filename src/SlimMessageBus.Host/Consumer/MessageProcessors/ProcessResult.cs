namespace SlimMessageBus.Host;

public enum ProcessResult
{
    Abandon,
    Fail,
    Retry,
    Success
}
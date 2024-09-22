namespace SlimMessageBus.Host;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A set of platform extensions to backfill functionality for some of the missing API prior in .NET 8.0.
/// </summary>
public static class PlatformExtensions
{
#if !NET8_0_OR_GREATER
    [ExcludeFromCodeCoverage]
    public static Task CancelAsync(this CancellationTokenSource cts)
    {
        cts.Cancel();
        return Task.CompletedTask;
    }
#endif
}
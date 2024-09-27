namespace SlimMessageBus.Host.Outbox;

static internal class IEnumerableExtensions
{
    public static IEnumerable<IReadOnlyCollection<T>> Batch<T>(this IEnumerable<T> items, int batchSize)
    {
        // Parameter validation in yielding methods should be wrapped
        Check(items, batchSize);

        using var enumerator = items.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var batch = new List<T>(batchSize) { enumerator.Current };
            for (var i = 1; i < batchSize && enumerator.MoveNext(); i++)
            {
                batch.Add(enumerator.Current);
            }

            yield return batch.AsReadOnly();
        }
    }

    private static void Check<T>(IEnumerable<T> items, int batchSize)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }
    }
}

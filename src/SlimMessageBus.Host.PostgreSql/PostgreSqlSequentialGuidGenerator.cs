namespace SlimMessageBus.Host.PostgreSql;

public class PostgreSqlSequentialGuidGenerator : IGuidGenerator
{
    public Guid NewGuid()
    {
        var guidBytes = Guid.NewGuid().ToByteArray();
        var timestamp = BitConverter.GetBytes(DateTime.UtcNow.Ticks);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestamp);
        }

        Buffer.BlockCopy(timestamp, 2, guidBytes, 10, 6);
        return new Guid(guidBytes);
    }
}

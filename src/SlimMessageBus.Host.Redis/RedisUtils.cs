namespace SlimMessageBus.Host.Redis;

static internal class RedisUtils
{
    /// <summary>
    /// Detect if wildcard is used
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    static internal RedisChannel ToRedisChannel(string path) => path.Contains('*') ? RedisChannel.Pattern(path) : RedisChannel.Literal(path);
}
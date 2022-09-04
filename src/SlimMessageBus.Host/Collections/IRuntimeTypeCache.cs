namespace SlimMessageBus.Host.Collections;

public interface IRuntimeTypeCache
{
    bool IsAssignableFrom(Type from, Type to);
}

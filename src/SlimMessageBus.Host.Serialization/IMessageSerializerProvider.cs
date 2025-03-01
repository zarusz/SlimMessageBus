namespace SlimMessageBus.Host.Serialization;

public interface IMessageSerializerProvider
{
    /// <summary>
    /// Obtains the serializer for the given path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    IMessageSerializer GetSerializer(string path);
}

namespace SlimMessageBus.Host.AmazonSQS;

public interface ISqsHeaderSerializer<THeaderValue> where THeaderValue : class
{
    THeaderValue Serialize(string key, object value);
    object Deserialize(string key, THeaderValue value);
}


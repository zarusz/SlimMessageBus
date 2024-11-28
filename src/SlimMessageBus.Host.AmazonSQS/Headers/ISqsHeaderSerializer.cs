namespace SlimMessageBus.Host.AmazonSQS;

public interface ISqsHeaderSerializer
{
    MessageAttributeValue Serialize(string key, object value);
    object Deserialize(string key, MessageAttributeValue value);
}

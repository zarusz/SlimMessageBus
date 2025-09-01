namespace SlimMessageBus.Host.AmazonSQS;

using MessageAttributeValue = Amazon.SQS.Model.MessageAttributeValue;

public class DefaultSqsHeaderSerializer(bool detectStringType = true) : ISqsHeaderSerializer<MessageAttributeValue>
{
    const string DataTypeNumber = "Number";
    const string DataTypeString = "String";

    public MessageAttributeValue Serialize(string key, object value) => value switch
    {
        // See more https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes                    
        var x when x is long || x is int || x is short || x is byte => new MessageAttributeValue
        {
            DataType = DataTypeNumber,
            StringValue = value.ToString()
        },
        _ => new MessageAttributeValue
        {
            DataType = DataTypeString,
            StringValue = value?.ToString()
        }
    };

    public object Deserialize(string key, MessageAttributeValue value) => value.DataType switch
    {
        DataTypeNumber when long.TryParse(value.StringValue, out var longValue) => longValue,
        DataTypeString when detectStringType && key != ReqRespMessageHeaders.RequestId && Guid.TryParse(value.StringValue, out var guid) => guid,
        DataTypeString when detectStringType && bool.TryParse(value.StringValue, out var b) => b,
        DataTypeString when detectStringType && DateTime.TryParse(value.StringValue, out var dt) => dt,
        DataTypeString => value.StringValue,
        _ => null
    };
}

namespace SlimMessageBus.Host.AmazonSQS;

using System.Globalization;

public class DefaultSnsHeaderSerializer(bool detectStringType = true) : ISqsHeaderSerializer<Amazon.SimpleNotificationService.Model.MessageAttributeValue>
{
    const string DataTypeNumber = "Number";
    const string DataTypeString = "String";

    public Amazon.SimpleNotificationService.Model.MessageAttributeValue Serialize(string key, object value) => value switch
    {
        // See more https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-message-metadata.html#sqs-message-attributes                    
        var x when x is long || x is int || x is short || x is byte => new Amazon.SimpleNotificationService.Model.MessageAttributeValue
        {
            DataType = DataTypeNumber,
            StringValue = value.ToString()
        },
        _ => new Amazon.SimpleNotificationService.Model.MessageAttributeValue
        {
            DataType = DataTypeString,
            StringValue = value?.ToString()
        }
    };

    public object Deserialize(string key, Amazon.SimpleNotificationService.Model.MessageAttributeValue value) => value.DataType switch
    {
        DataTypeNumber when long.TryParse(value.StringValue, out var longValue) => longValue,
        DataTypeString when detectStringType && key != ReqRespMessageHeaders.RequestId && Guid.TryParse(value.StringValue, out var guid) => guid,
        DataTypeString when detectStringType && bool.TryParse(value.StringValue, out var b) => b,
        DataTypeString when detectStringType && DateTime.TryParse(value.StringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) => dt,
        DataTypeString => value.StringValue,
        _ => null
    };
}

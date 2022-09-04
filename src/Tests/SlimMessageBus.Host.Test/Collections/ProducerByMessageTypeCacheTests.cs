namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;
using Microsoft.Extensions.Logging.Abstractions;

public class ProducerByMessageTypeCacheTests
{
    public static IEnumerable<object[]> Data => new List<object[]>
    {
        new object[] { typeof(Message), typeof(Message) },
        new object[] { typeof(AMessage), typeof(AMessage) },
        new object[] { typeof(BMessage), typeof(Message)},
        new object[] { typeof(B1Message), typeof(Message) },
        new object[] { typeof(B2Message), typeof(B2Message) },
        new object[] { typeof(ISomeMessage), typeof(ISomeMessage) },
        new object[] { typeof(SomeMessage), typeof(ISomeMessage) },
        new object[] { typeof(object), null },
        new object[] { typeof(int), null },
    };

    [Theory]
    [MemberData(nameof(Data))]
    public void Given_ComplexMessageTypeHierarchy_When_GetProducer_Then_ResolvesProperTypeOrNullWhenNoMatch(Type messageType, Type expectedProducerMessageType)
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        mbb.Produce<Message>(x => x.DefaultTopic("t1"));
        mbb.Produce<AMessage>(x => x.DefaultTopic("t2"));
        mbb.Produce<B2Message>(x => x.DefaultTopic("t3"));
        mbb.Produce<ISomeMessage>(x => x.DefaultTopic("t4"));

        var producerByBaseMessageType = mbb.Settings.Producers.ToDictionary(x => x.MessageType);

        var subject = new ProducerByMessageTypeCache<ProducerSettings>(
            NullLogger.Instance,
            producerByBaseMessageType,
            new RuntimeTypeCache());

        // act

        // assert

        if (expectedProducerMessageType != null)
        {
            subject[messageType].Should().BeSameAs(producerByBaseMessageType[expectedProducerMessageType]);
        }
        else
        {
            subject[messageType].Should().BeNull();
        }

        subject[typeof(Message)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);

        subject[typeof(AMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(AMessage)]);
        subject[typeof(BMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);

        subject[typeof(B1Message)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);
        subject[typeof(B2Message)].Should().BeSameAs(producerByBaseMessageType[typeof(B2Message)]);

        subject[typeof(ISomeMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(ISomeMessage)]);
        subject[typeof(SomeMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(ISomeMessage)]);

        subject[typeof(object)].Should().BeNull();
        subject[typeof(int)].Should().BeNull();
    }

    internal record Message;
    internal record AMessage : Message, ISomeMessage;
    internal record BMessage : Message;
    internal record B1Message : BMessage;
    internal record B2Message : BMessage;
    internal interface ISomeMessage { }
    internal record SomeMessage : ISomeMessage;
}

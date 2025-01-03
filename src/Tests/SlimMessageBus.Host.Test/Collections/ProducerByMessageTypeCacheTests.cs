namespace SlimMessageBus.Host.Test.Collections;

using Microsoft.Extensions.Logging.Abstractions;

using SlimMessageBus.Host.Collections;

public class ProducerByMessageTypeCacheTests
{
    public static IEnumerable<object[]> Data =>
    [
        [typeof(Message), typeof(Message)],

        [typeof(AMessage), typeof(AMessage)],

        [typeof(BMessage), typeof(Message)],
        [typeof(B1Message), typeof(Message)],

        [typeof(B2Message), typeof(B2Message)],

        [typeof(ISomeMessage), typeof(ISomeMessage)],
        [typeof(SomeMessage), typeof(ISomeMessage)],

        [typeof(object), null],
        [typeof(int), null],

        [typeof(IEnumerable<Message>), typeof(Message)],
        [typeof(Message[]), typeof(Message)],

        [typeof(IEnumerable<BMessage>), typeof(Message)],
        [typeof(BMessage[]), typeof(Message)],

        [typeof(IEnumerable<B1Message>), typeof(Message)]        ,
        [typeof(B1Message[]), typeof(Message)],
    ];

    [Theory]
    [MemberData(nameof(Data))]
    public void Given_ComplexMessageTypeHierarchy_When_GetProducer_Then_ResolvesProperTypeOrNullWhenNoMatch(Type messageType, Type expectedProducerMessageType)
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        mbb.Produce<Message>(x => x.DefaultPath("t1"));
        mbb.Produce<AMessage>(x => x.DefaultPath("t2"));
        mbb.Produce<B2Message>(x => x.DefaultPath("t3"));
        mbb.Produce<ISomeMessage>(x => x.DefaultPath("t4"));

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
    }

    internal record Message;
    internal record AMessage : Message, ISomeMessage;
    internal record BMessage : Message;
    internal record B1Message : BMessage;
    internal record B2Message : BMessage;
    internal interface ISomeMessage { }
    internal record SomeMessage : ISomeMessage;
}

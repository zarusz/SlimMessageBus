namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;
using SlimMessageBus.Host.Config;
using Microsoft.Extensions.Logging.Abstractions;

public class ProducerByMessageTypeCacheTests
{
    [Fact]
    public void Given_ComplexMessageTypeHierarchy_When_GetProducer_Then_ResolvesProperTypeOrExceptionWhenNoMatch()
    {
        // arrange
        var mbb = MessageBusBuilder.Create();
        mbb.Produce<Message>(x => x.DefaultTopic("t1"));
        mbb.Produce<AMessage>(x => x.DefaultTopic("t2"));
        mbb.Produce<B2Message>(x => x.DefaultTopic("t3"));

        var producerByBaseMessageType = mbb.Settings.Producers.ToDictionary(x => x.MessageType);

        var subject = new ProducerByMessageTypeCache<ProducerSettings>(
            NullLogger.Instance,
            producerByBaseMessageType);

        // act

        // assert
        subject[typeof(Message)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);

        subject[typeof(AMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(AMessage)]);
        subject[typeof(BMessage)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);

        subject[typeof(B1Message)].Should().BeSameAs(producerByBaseMessageType[typeof(Message)]);
        subject[typeof(B2Message)].Should().BeSameAs(producerByBaseMessageType[typeof(B2Message)]);

        subject[typeof(object)].Should().BeNull();
        subject[typeof(int)].Should().BeNull();
    }

    internal class Message { }
    internal class AMessage : Message { }
    internal class BMessage : Message { }
    internal class B1Message : BMessage { }
    internal class B2Message : BMessage { }
}

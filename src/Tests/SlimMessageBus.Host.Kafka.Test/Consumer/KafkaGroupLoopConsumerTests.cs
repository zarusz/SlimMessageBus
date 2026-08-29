namespace SlimMessageBus.Host.Kafka.Test;

public class KafkaLoopGroupConsumerTests
{
    private readonly KafkaGroupConsumer _subject;
    
    private const string GROUP = "group-a";
    
    private const string TOPIC = "topic-a";
    
    private readonly Mock<IKafkaLoopFailureInterceptor> _mockFailureInterceptor;
    
    private readonly Exception _expectedException = new InvalidOperationException("Test fatal error");
    
    public KafkaLoopGroupConsumerTests()
    {
        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;

        var consumerSettings = new List<AbstractConsumerSettings>
        {
            new ConsumerSettings
            {
                Path = TOPIC
            }
        }.AsReadOnly();
        
        var mockKafkaConsumer = new Mock<IConsumer<Ignore, byte[]>>();
        
        mockKafkaConsumer
            .Setup(x => x.Consume(It.IsAny<CancellationToken>()))
            .Throws(_expectedException);
        
        var mockProcessorFactory = new Mock<Func<TopicPartition, IKafkaCommitController, IKafkaPartitionConsumer>>();
        _mockFailureInterceptor = new Mock<IKafkaLoopFailureInterceptor>();
        
        var mockConsumerBuilder = new Mock<ConsumerBuilder<Ignore, byte[]>>(new Dictionary<string, string>() );
        
        mockConsumerBuilder
            .Setup(builder => builder.Build()) 
            .Returns(mockKafkaConsumer.Object);
        
        var mockConsumerBuilderFactory = new Mock<Func<KafkaClientConfig<ConsumerConfig>, ConsumerBuilder<Ignore, byte[]>>>(); 
        mockConsumerBuilderFactory
            .Setup(factory => factory(It.IsAny<KafkaClientConfig<ConsumerConfig>>()))
            .Returns(mockConsumerBuilder.Object);
        
        var settings = new KafkaMessageBusSettings
        {
            ConsumerBuilderFactory = mockConsumerBuilderFactory.Object
        };
        
        _subject = new KafkaGroupConsumer(loggerFactory,
            settings,
            consumerSettings,
            [],
            [_mockFailureInterceptor.Object],
            GROUP,
            [TOPIC], mockProcessorFactory.Object);
    }

    [Fact]
    public async Task When_LoopExceptionThrown_Then_ShouldIntercept()
    {
        await _subject.Start();
        await Task.Delay(100); 

        await _subject.Stop();

        _mockFailureInterceptor.Verify(
            x => x.OnFailureAsync(It.Is<KafkaLoopFailureContext>(ctx =>
                ctx.Group == GROUP && ctx.Exception == _expectedException)),
            Times.Once,
            "Expected IKafkaGroupLoopFailureInterceptor.OnFailureAsync to be called once."
        );
    }

}
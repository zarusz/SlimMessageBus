namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;

public class RuntimeTypeCacheTest
{
    [Theory]
    [InlineData(typeof(bool), typeof(int), false)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IConsumer<SomeMessage>), true)]
    [InlineData(typeof(SomeMessageConsumer), typeof(IRequestHandler<SomeRequest, SomeResponse>), false)]
    [InlineData(typeof(IConsumer<SomeMessage>), typeof(SomeMessageConsumer), false)]
    public void IsAssignableFromWorks(Type from, Type to, bool expectedResult)
    {
        // arrange
        var subject = new RuntimeTypeCache();

        // act
        var result = subject.IsAssignableFrom(from, to);

        // assert
        result.Should().Be(expectedResult);
    }
}

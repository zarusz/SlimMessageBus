namespace SlimMessageBus.Host.Test.MessageTypeResolver;

public class AssemblyQualifiedNameMessageTypeResolverTests
{
    private readonly Mock<IAssemblyQualifiedNameMessageTypeResolverRedirect> _redirect;
    private readonly AssemblyQualifiedNameMessageTypeResolver _subject;

    public AssemblyQualifiedNameMessageTypeResolverTests()
    {
        _redirect = new Mock<IAssemblyQualifiedNameMessageTypeResolverRedirect>();
        _subject = new AssemblyQualifiedNameMessageTypeResolver([_redirect.Object]);
    }

    [Theory]
    [InlineData(typeof(SomeMessage), "SlimMessageBus.Host.Test.SomeMessage, SlimMessageBus.Host.Test")]
    public void When_ToName_Given_TypeIsProvided_Then_AssemblyQualifiedNameIsReturned(Type messageType, string expectedName)
    {
        // Arrange
        _redirect.Setup(x => x.TryGetName(messageType)).Returns(expectedName);

        // Act
        var result = _subject.ToName(messageType);

        // Assert
        result.Should().Be(expectedName);
    }

    [Theory]
    [InlineData("SlimMessageBus.Host.Test.SomeMessage, SlimMessageBus.Host.Test", typeof(SomeMessage))]
    [InlineData("SlimMessageBus.Host.Test.SomeMessageV1, SlimMessageBus.Host.Test", typeof(SomeMessage))]
    [InlineData("SlimMessageBus.Host.Test.SomeMessage, SlimMessageBus.Host.V1", typeof(SomeMessage))]
    public void When_ToType_Given_NameIsValid_Then_TypeIsReturned(string name, Type expectedMessageType)
    {
        // Arrange
        _redirect.Setup(x => x.TryGetType("SlimMessageBus.Host.Test.SomeMessageV1, SlimMessageBus.Host.Test")).Returns(typeof(SomeMessage));
        _redirect.Setup(x => x.TryGetType("SlimMessageBus.Host.Test.SomeMessage, SlimMessageBus.Host.V1")).Returns(typeof(SomeMessage));

        // Act
        var type = _subject.ToType(name);

        // Assert
        type.Should().Be(expectedMessageType);
    }

}

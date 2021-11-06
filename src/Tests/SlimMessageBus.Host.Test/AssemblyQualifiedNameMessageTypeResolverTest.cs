namespace SlimMessageBus.Host.Test
{
    using FluentAssertions;
    using Xunit;

    public class AssemblyQualifiedNameMessageTypeResolverTest
    {
        [Fact]
        public void ItWorks()
        {
            // arrange
            var subject = new AssemblyQualifiedNameMessageTypeResolver();
            var name = subject.ToName(typeof(SomeMessage));

            // act
            var type = subject.ToType(name);

            // assert
            type.Should().Be(typeof(SomeMessage));
        }
    }
}

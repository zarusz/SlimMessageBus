namespace SlimMessageBus.Host.Test
{
    using FluentAssertions;
    using SlimMessageBus.Host.Test.Messages;
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class AssemblyQualifiedNameMessageTypeResolverTest
    {
        [Theory]
        [InlineData(typeof(SomeMessage))]
        [InlineData(typeof((int Key, string Value)))]
        [InlineData(typeof(SomeMessage2))]
        [InlineData(typeof(int))]
        public void ConvertsAnyTypeBackAndForth(Type messageType)
        {
            // arrange
            var subject = new AssemblyQualifiedNameMessageTypeResolver();
            var name = subject.ToName(messageType);

            // act
            var type = subject.ToType(name);

            // assert
            type.Should().Be(messageType);
        }

        [Fact]
        public void ConvertsTypeToName()
        {
            // arrange
            var subject = new AssemblyQualifiedNameMessageTypeResolver();

            // act
            var name = subject.ToName(typeof(IEnumerable<SomeMessage2>));

            // assert
            name.Should().Be("System.Collections.Generic.IEnumerable`1[[SlimMessageBus.Host.Test.Messages.SomeMessage2, SlimMessageBus.Host.Test.Common]], System.Private.CoreLib");
        }

        [Theory]
        [InlineData("SlimMessageBus.Host.Test.Messages.SomeMessage2, SlimMessageBus.Host.Test.Common", typeof(SomeMessage2))]
        [InlineData("System.Collections.Generic.IEnumerable`1[[SlimMessageBus.Host.Test.Messages.SomeMessage2, SlimMessageBus.Host.Test.Common]], System.Private.CoreLib", typeof(IEnumerable<SomeMessage2>))]
        public void ConvertsNameToType(string name, Type messageType)
        {
            // arrange
            var subject = new AssemblyQualifiedNameMessageTypeResolver();

            // act
            var type = subject.ToType(name);

            // assert
            type.Should().Be(messageType);
        }

        [Theory]
        [InlineData("System.Collections.Generic.IEnumerable`1[[SlimMessageBus.Host.Test.Messages.SomeMessage3, SlimMessageBus.Host.Test.Common]], System.Private.CoreLib")]
        [InlineData("System.Collections.Generic.IEnumerable`1[[SlimMessageBus.Host.Test.Messages.SomeMessage2]], System.Private.CoreLib")]
        public void ConvertsInvalidNameToNullType(string name)
        {
            // arrange
            var subject = new AssemblyQualifiedNameMessageTypeResolver();

            // act
            var type = subject.ToType(name);

            // assert
            type.Should().BeNull();
        }
    }
}

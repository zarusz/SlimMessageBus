using System;
using System.Threading;
using Moq;
using SlimMessageBus.Host.Collections;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace SlimMessageBus.Host.Test.Collections
{
    public class SafeDictionaryWrapperTest
    {
        [Fact]
        public void GetOrAddWorks()
        {
            // arrange
            var w = new SafeDictionaryWrapper<string, string>();
            var v = "2";
            var k = "a";
            var valueFactoryMock = new Mock<Func<string, string>>();
            valueFactoryMock.Setup(x => x(k)).Returns(v);
                
            // act
            var v1 = w.GetOrAdd(k, valueFactoryMock.Object);
            var v2 = w.GetOrAdd(k, valueFactoryMock.Object);

            // assert
            w.Dictonary.Count.Should().Be(1);

            v1.Should().BeEquivalentTo(v);
            v2.Should().BeEquivalentTo(v);
            w.Dictonary[k].Should().BeEquivalentTo(v);

            valueFactoryMock.Verify(x => x(k), Times.Once);
        }

        [Fact]
        public void ClearWorks()
        {
            var w = new SafeDictionaryWrapper<string, string>();
            w.GetOrAdd("a", x => "a");
            w.GetOrAdd("b", x => "b");

            // act
            w.Clear();

            // assert
            w.Dictonary.Count.Should().Be(0);
        }

        [Fact]
        public void CannotMutateDictionary()
        {
            var w = new SafeDictionaryWrapper<string, string>();
            w.GetOrAdd("a", x => "a");
            w.GetOrAdd("b", x => "b");

            // act
            Action clearAction = () => w.Dictonary.Clear();
            Action addAction = () => w.Dictonary.Add("c", "c");

            // assert
            clearAction.Should().Throw<NotSupportedException>();
            addAction.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void CheckThreadSafety()
        {
            // arrange
            var w = new SafeDictionaryWrapper<string, string>();

            var count = 3000;

            // act
            var task1 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                {
                    w.GetOrAdd($"a_{i}", k => $"v_{i}");
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

            var task2 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                {
                    w.GetOrAdd($"b_{i}", k => $"v_{i}");
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);

            var task3 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                {
                    w.GetOrAdd($"c_{i}", k => $"v_{i}");
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            Task.WaitAll(task1, task2, task3);

            // assert
            w.Dictonary.Count.Should().Be(3 * count);
        }
    }
}

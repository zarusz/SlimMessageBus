using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SlimMessageBus.Host.Collections;
using System.Threading.Tasks;

namespace SlimMessageBus.Host.Test.Collections
{
    [TestClass]
    public class SafeDictionaryWrapperTest
    {
        [TestMethod]
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
            w.Dictonary.Count.ShouldBeEquivalentTo(1);

            v1.ShouldBeEquivalentTo(v);
            v2.ShouldBeEquivalentTo(v);
            w.Dictonary[k].ShouldBeEquivalentTo(v);

            valueFactoryMock.Verify(x => x(k), Times.Once);
        }

        [TestMethod]
        public void ClearWorks()
        {
            var w = new SafeDictionaryWrapper<string, string>();
            w.GetOrAdd("a", x => "a");
            w.GetOrAdd("b", x => "b");

            // act
            w.Clear();

            // assert
            w.Dictonary.Count.ShouldBeEquivalentTo(0);
        }

        [TestMethod]
        public void CannotMutateDictionary()
        {
            var w = new SafeDictionaryWrapper<string, string>();
            w.GetOrAdd("a", x => "a");
            w.GetOrAdd("b", x => "b");

            // act
            Action clearAction = () => w.Dictonary.Clear();
            Action addAction = () => w.Dictonary.Add("c", "c");

            // assert
            clearAction.ShouldThrow<NotSupportedException>();
            addAction.ShouldThrow<NotSupportedException>();
        }

        [TestMethod]
        public void CheckThreadSafety()
        {
            // arrange
            var w = new SafeDictionaryWrapper<string, string>();

            var count = 3000;

            // act
            var task1 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                    w.GetOrAdd($"a_{i}", k => $"v_{i}");
            });
            var task2 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                    w.GetOrAdd($"b_{i}", k => $"v_{i}");
            });
            var task3 = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < count; i++)
                    w.GetOrAdd($"c_{i}", k => $"v_{i}");
            });
            Task.WaitAll(task1, task2, task3);

            // assert
            w.Dictonary.Count.ShouldBeEquivalentTo(3 * count);
        }
    }
}

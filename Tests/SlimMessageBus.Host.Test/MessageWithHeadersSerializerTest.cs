using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SlimMessageBus.Host.Test
{
    [TestClass]
    public class MessageWithHeadersSerializerTest
    {
        private byte[] _payload;
        private MessageWithHeadersSerializer _serializer;

        [TestInitialize]
        public void Setup()
        {
            _payload = Encoding.UTF8.GetBytes("Sample message payload in UTF8");
            _serializer = new MessageWithHeadersSerializer(Encoding.ASCII);
        }

        [TestMethod]
        public void WithoutHeaders_SerializationWorks()
        {
            // arrange
            var m = new MessageWithHeaders
            {
                Payload = _payload
            };

            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            Assert.AreEqual(0, m2.Headers.Count);
            Assert.IsTrue(_payload.SequenceEqual(m2.Payload));
        }

        [TestMethod]
        public void WithHeaders_SerializationWorks()
        {
            // arrange
            var m = new MessageWithHeaders
            {
                Payload = _payload,
                Headers =
                {
                    {"key1", "value1"},
                    {"key2", "value22"}
                }
            };

            // act
            var payload = _serializer.Serialize(typeof(MessageWithHeaders), m);
            var m2 = (MessageWithHeaders)_serializer.Deserialize(typeof(MessageWithHeaders), payload);

            // assert
            Assert.AreEqual(2, m2.Headers.Count);
            Assert.IsTrue(m2.Headers.ContainsKey("key1"));
            Assert.AreEqual("value1", m2.Headers["key1"]);
            Assert.IsTrue(m2.Headers.ContainsKey("key2"));
            Assert.AreEqual("value22", m2.Headers["key2"]);
            Assert.IsTrue(_payload.SequenceEqual(m2.Payload));
        }
    }
}

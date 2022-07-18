namespace SlimMessageBus.Host.Serialization.Benchmark
{
    using global::Avro;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Order;
    using Microsoft.Extensions.Logging.Abstractions;
    using SlimMessageBus.Host.Serialization.Avro;
    using SlimMessageBus.Host.Serialization.Json;
    using System;
    using System.Collections.Generic;

    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class SerDesBenchmark
    {
        // public field
        [ParamsSource(nameof(Scenarios))]
        public Scenario scenario;

        public IEnumerable<Scenario> Scenarios => new[]
        {
            new Scenario("Json",
                new AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new JsonMessageSerializer()),
            new Scenario("NativeJson",
                new AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new SystemTextJson.JsonMessageSerializer()),
            new Scenario("Avro_Default",
                new Sample.Serialization.MessagesAvro.AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new AvroMessageSerializer(NullLoggerFactory.Instance)),
            new Scenario("Avro_NoReflection",
                new Sample.Serialization.MessagesAvro.AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new AvroMessageSerializer(NullLoggerFactory.Instance,
                    new DictionaryMessageCreationStategy(NullLogger<DictionaryMessageCreationStategy>.Instance, new Dictionary<Type, Func<object>>
                    {
                        [typeof(Sample.Serialization.MessagesAvro.AddCommand)] = () => new Sample.Serialization.MessagesAvro.AddCommand()
                    }),
                    new DictionarySchemaLookupStrategy(NullLogger<DictionarySchemaLookupStrategy>.Instance, new Dictionary<Type, Schema>
                    {
                        [typeof(Sample.Serialization.MessagesAvro.AddCommand)] = Sample.Serialization.MessagesAvro.AddCommand._SCHEMA
                    })
                ))
        };

        [Benchmark]
        public void SerDes()
        {
            var payload = scenario.Serializer.Serialize(scenario.MessageType, scenario.Message);
            scenario.Serializer.Deserialize(scenario.MessageType, payload);
        }

        public class Scenario
        {
            public string Name;
            public object Message;
            public Type MessageType;
            public IMessageSerializer Serializer;

            public Scenario(string name, object message, IMessageSerializer serializer)
            {
                Message = message;
                MessageType = message.GetType();
                Serializer = serializer;
                Name = name;
            }

            public override string ToString() => Name;
        }
    }
}

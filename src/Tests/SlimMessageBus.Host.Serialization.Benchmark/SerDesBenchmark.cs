using Avro;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using SlimMessageBus.Host.Serialization.Avro;
using SlimMessageBus.Host.Serialization.AvroConvert;
using SlimMessageBus.Host.Serialization.Json;
using SolTechnology.Avro.Codec;
using System;
using System.Collections.Generic;

namespace SlimMessageBus.Host.Serialization.Benchmark
{
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
            new Scenario("AvroConvert_Default",
                new AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new AvroConvertMessageSerializer()),
            new Scenario("Avro_Default",
                new Sample.AvroSer.Messages.ContractFirst.AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new AvroMessageSerializer()),
            new Scenario("Avro_NoReflection",
                new Sample.AvroSer.Messages.ContractFirst.AddCommand { OperationId = Guid.NewGuid().ToString(), Left = 100, Right = 200 },
                new AvroMessageSerializer(
                    new DictionaryMessageCreationStategy(new Dictionary<Type, Func<object>>
                    {
                        [typeof(Sample.AvroSer.Messages.ContractFirst.AddCommand)] = () => new Sample.AvroSer.Messages.ContractFirst.AddCommand()
                    }),
                    new DictionarySchemaLookupStrategy(new Dictionary<Type, Schema>
                    {
                        [typeof(Sample.AvroSer.Messages.ContractFirst.AddCommand)] = Sample.AvroSer.Messages.ContractFirst.AddCommand._SCHEMA
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

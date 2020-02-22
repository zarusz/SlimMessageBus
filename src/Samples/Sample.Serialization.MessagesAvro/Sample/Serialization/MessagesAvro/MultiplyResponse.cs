// ------------------------------------------------------------------------------
// <auto-generated>
//    Generated by avrogen, version 1.9.0.0
//    Changes to this file may cause incorrect behavior and will be lost if code
//    is regenerated
// </auto-generated>
// ------------------------------------------------------------------------------
namespace Sample.Serialization.MessagesAvro
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Avro;
	using Avro.Specific;
	
	public partial class MultiplyResponse : ISpecificRecord
	{
		public static Schema _SCHEMA = Avro.Schema.Parse("{\"type\":\"record\",\"name\":\"MultiplyResponse\",\"namespace\":\"Sample.Serialization.Mess" +
				"agesAvro\",\"fields\":[{\"name\":\"OperationId\",\"type\":\"string\"},{\"name\":\"Result\",\"typ" +
				"e\":\"int\"}]}");
		private string _OperationId;
		private int _Result;
		public virtual Schema Schema
		{
			get
			{
				return MultiplyResponse._SCHEMA;
			}
		}
		public string OperationId
		{
			get
			{
				return this._OperationId;
			}
			set
			{
				this._OperationId = value;
			}
		}
		public int Result
		{
			get
			{
				return this._Result;
			}
			set
			{
				this._Result = value;
			}
		}
		public virtual object Get(int fieldPos)
		{
			switch (fieldPos)
			{
			case 0: return this.OperationId;
			case 1: return this.Result;
			default: throw new AvroRuntimeException("Bad index " + fieldPos + " in Get()");
			};
		}
		public virtual void Put(int fieldPos, object fieldValue)
		{
			switch (fieldPos)
			{
			case 0: this.OperationId = (System.String)fieldValue; break;
			case 1: this.Result = (System.Int32)fieldValue; break;
			default: throw new AvroRuntimeException("Bad index " + fieldPos + " in Put()");
			};
		}
	}
}
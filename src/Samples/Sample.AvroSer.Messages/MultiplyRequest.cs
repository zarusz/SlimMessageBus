using SlimMessageBus;

namespace Sample.AvroSer.Messages
{
    /// <summary>
    /// Adds the marker interface to the generated class <see cref="MultiplyRequest"/>.
    /// </summary>    
    public partial class MultiplyRequest : IRequestMessage<MultiplyResponse>
    {
    }
}

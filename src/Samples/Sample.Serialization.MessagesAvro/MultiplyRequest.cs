using SlimMessageBus;

namespace Sample.Serialization.MessagesAvro
{
    /// <summary>
    /// Adds the marker interface to the generated class <see cref="MultiplyRequest"/>.
    /// </summary>    
    public partial class MultiplyRequest : IRequestMessage<MultiplyResponse>
    {
    }
}

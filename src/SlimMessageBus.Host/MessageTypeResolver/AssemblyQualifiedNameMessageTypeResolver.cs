namespace SlimMessageBus.Host
{
    using System;

    /// <summary>
    /// <see cref="IMessageTypeResolver"/> that uses the <see cref="Type.AssemblyQualifiedName"/> for the message type string passed in the message header.
    /// </summary>
    public class AssemblyQualifiedNameMessageTypeResolver : IMessageTypeResolver
    {
        public string ToName(Type messageType) => messageType?.AssemblyQualifiedName ?? throw new ArgumentNullException(nameof(messageType));

        public Type ToType(string name) => Type.GetType(name ?? throw new ArgumentNullException(nameof(name)));
    }
}
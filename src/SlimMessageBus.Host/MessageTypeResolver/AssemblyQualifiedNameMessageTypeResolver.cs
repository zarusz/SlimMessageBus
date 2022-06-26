namespace SlimMessageBus.Host
{
    using SlimMessageBus.Host.Collections;
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// <see cref="IMessageTypeResolver"/> that uses the <see cref="Type.AssemblyQualifiedName"/> for the message type string passed in the message header.
    /// </summary>
    public class AssemblyQualifiedNameMessageTypeResolver : IMessageTypeResolver
    {
        private static readonly Regex RedundantAssemblyTokens = new(@"\, (Version|Culture|PublicKeyToken)\=([\w\d.]+)");

        /// <summary>
        /// Determines wheather to emit the Version, Culture and PublicKeyToken along with the Assembly name (for strong assembly naming).
        /// </summary>
        public bool EmitAssemblyStrongName { get; set; } = false;

        private readonly SafeDictionaryWrapper<Type, string> toNameCache;
        private readonly SafeDictionaryWrapper<string, Type> toTypeCache;

        public AssemblyQualifiedNameMessageTypeResolver()
        {
            toNameCache = new SafeDictionaryWrapper<Type, string>(ToNameInternal);
            toTypeCache = new SafeDictionaryWrapper<string, Type>(ToTypeInternal);
        }

        private string ToNameInternal(Type messageType)
        {
            var assemblyQualifiedName = messageType?.AssemblyQualifiedName ?? throw new ArgumentNullException(nameof(messageType));

            if (EmitAssemblyStrongName)
            {
                return assemblyQualifiedName;
            }

            var reducedName = RedundantAssemblyTokens.Replace(assemblyQualifiedName, string.Empty);

            return reducedName;
        }

        private Type ToTypeInternal(string name) => Type.GetType(name ?? throw new ArgumentNullException(nameof(name)));

        public string ToName(Type messageType) => toNameCache.GetOrAdd(messageType);

        public Type ToType(string name) => toTypeCache.GetOrAdd(name);
    }
}
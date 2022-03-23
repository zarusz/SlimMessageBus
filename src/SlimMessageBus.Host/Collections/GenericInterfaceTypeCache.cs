namespace SlimMessageBus.Host.Collections
{
    using SlimMessageBus.Host.DependencyResolver;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class GenericInterfaceTypeCache
    {
        private readonly Type openGenericType;
        private readonly string methodName;
        private readonly SafeDictionaryWrapper<Type, GenericInterfaceType> messageTypeToGenericInterfaceType;
        private readonly SafeDictionaryWrapper<Type, bool> messageTypeToResolveIsEmpty;

        public GenericInterfaceTypeCache(Type openGenericType, string methodName)
        {
            this.openGenericType = openGenericType;
            this.methodName = methodName;
            messageTypeToGenericInterfaceType = new SafeDictionaryWrapper<Type, GenericInterfaceType>(CreateInterceptorType);
            messageTypeToResolveIsEmpty = new SafeDictionaryWrapper<Type, bool>();
        }

        private GenericInterfaceType CreateInterceptorType(Type messageType)
        {
            var genericType = openGenericType.MakeGenericType(messageType);
            var method = genericType.GetMethod(methodName);
            return new GenericInterfaceType(messageType, genericType, method);
        }

        /// <summary>
        /// Returns the resolved instances, or null if none are registered.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public IEnumerable<object> ResolveAll(IDependencyResolver scope, Type messageType)
        {
            var git = Get(messageType);

            var cacheExists = messageTypeToResolveIsEmpty.TryGet(messageType, out var resolveIsEmpty);
            if (!cacheExists || !resolveIsEmpty)
            {
                var interceptors = (IEnumerable<object>)scope.Resolve(git.EnumerableOfGenericType);

                if (!cacheExists)
                {
                    resolveIsEmpty = interceptors == null || !interceptors.Any();
                    messageTypeToResolveIsEmpty.Set(messageType, resolveIsEmpty);
                }

                if (!resolveIsEmpty)
                {
                    return interceptors;
                }
            }
            return null;
        }

        public GenericInterfaceType Get(Type messageType) => messageTypeToGenericInterfaceType.GetOrAdd(messageType);

        public class GenericInterfaceType
        {
            public Type MessageType { get; }
            public Type GenericType { get; }
            public Type EnumerableOfGenericType { get; }
            public MethodInfo Method { get; }

            public GenericInterfaceType(Type messageType, Type genericType, MethodInfo method)
            {
                MessageType = messageType;
                GenericType = genericType;
                EnumerableOfGenericType = typeof(IEnumerable<>).MakeGenericType(genericType);
                Method = method;
            }
        }
    }
}

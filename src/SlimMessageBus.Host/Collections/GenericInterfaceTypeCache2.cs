namespace SlimMessageBus.Host.Collections
{
    using SlimMessageBus.Host.DependencyResolver;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class GenericInterfaceTypeCache2
    {
        public record struct RequestResponseTypes(Type RequestType, Type ResponseType);

        private readonly Type openGenericType;
        private readonly string methodName;
        private readonly SafeDictionaryWrapper<RequestResponseTypes, GenericInterfaceType> messageTypeToGenericInterfaceType;
        private readonly SafeDictionaryWrapper<RequestResponseTypes, bool> messageTypeToResolveIsEmpty;

        public GenericInterfaceTypeCache2(Type openGenericType, string methodName)
        {
            this.openGenericType = openGenericType;
            this.methodName = methodName;
            messageTypeToGenericInterfaceType = new SafeDictionaryWrapper<RequestResponseTypes, GenericInterfaceType>(CreateInterceptorType);
            messageTypeToResolveIsEmpty = new SafeDictionaryWrapper<RequestResponseTypes, bool>();
        }

        private GenericInterfaceType CreateInterceptorType(RequestResponseTypes p)
        {
            var genericType = openGenericType.MakeGenericType(p.RequestType, p.ResponseType);
            var method = genericType.GetMethod(methodName);
            return new GenericInterfaceType(p.RequestType, p.ResponseType, genericType, method);
        }

        /// <summary>
        /// Returns the resolved instances, or null if none are registered.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public IEnumerable<object> ResolveAll(IDependencyResolver scope, Type requestType, Type responseType)
        {
            var p = new RequestResponseTypes(requestType, responseType);
            var git = messageTypeToGenericInterfaceType.GetOrAdd(p);

            var cacheExists = messageTypeToResolveIsEmpty.TryGet(p, out var resolveIsEmpty);
            if (!cacheExists || !resolveIsEmpty)
            {
                var interceptors = (IEnumerable<object>)scope.Resolve(git.EnumerableOfGenericType);

                if (!cacheExists)
                {
                    resolveIsEmpty = interceptors == null || !interceptors.Any();
                    messageTypeToResolveIsEmpty.Set(p, resolveIsEmpty);
                }

                if (!resolveIsEmpty)
                {
                    return interceptors;
                }
            }
            return null;
        }

        public GenericInterfaceType Get(Type requestType, Type responseType) => messageTypeToGenericInterfaceType.GetOrAdd(new (requestType, responseType));

        public class GenericInterfaceType
        {
            public Type RequestType { get; }
            public Type ResponseType { get; }
            public Type GenericType { get; }
            public Type EnumerableOfGenericType { get; }
            public MethodInfo Method { get; }

            public GenericInterfaceType(Type requestType, Type responseType, Type genericType, MethodInfo method)
            {
                RequestType = requestType;
                ResponseType = responseType;
                GenericType = genericType;
                EnumerableOfGenericType = typeof(IEnumerable<>).MakeGenericType(genericType);
                Method = method;
            }
        }

    }
}

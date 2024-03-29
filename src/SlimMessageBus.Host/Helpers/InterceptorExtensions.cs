﻿namespace SlimMessageBus.Host;

public static class InterceptorExtensions
{
    public static int GetOrder(this IInterceptor interceptor) => (interceptor as IInterceptorWithOrder)?.Order ?? 0;
}
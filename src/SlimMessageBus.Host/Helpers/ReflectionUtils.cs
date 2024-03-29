namespace SlimMessageBus.Host;

using System.Diagnostics;
using System.Linq.Expressions;

public static class ReflectionUtils
{
    public static Func<object, object> GenerateGetterExpr(PropertyInfo property)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, property.DeclaringType);

        var propertyExpr = Expression.Property(typedInstanceExpr, property);
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));

        return Expression.Lambda<Func<object, object>>(propertyObjExpr, objInstanceExpr).Compile();
    }

    public static Func<object, object> GenerateGetterFunc(PropertyInfo property)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, property.DeclaringType);

        var propertyExpr = Expression.Property(typedInstanceExpr, property);
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));

        return Expression.Lambda<Func<object, object>>(propertyObjExpr, objInstanceExpr).Compile();
    }

    public static T GenerateMethodCallToFunc<T>(MethodInfo method, Type instanceType, Type returnType, params Type[] argumentTypes)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.Convert(objInstanceExpr, instanceType);

        var objArguments = argumentTypes.Select((x, i) => Expression.Parameter(typeof(object), $"arg{i + 1}")).ToArray();
        var typedArguments = argumentTypes.Select((x, i) => Expression.Convert(objArguments[i], x)).ToArray();

        var methodResultExpr = Expression.Call(typedInstanceExpr, method, typedArguments);
        var typedMethodResultExpr = Expression.Convert(methodResultExpr, returnType);

        return Expression.Lambda<T>(typedMethodResultExpr, new[] { objInstanceExpr }.Concat(objArguments)).Compile();
    }

    /// <summary>
    /// Creates a delegate for the specified method wrapping both required and optional parameters. 
    /// 
    /// The first parameter in the delegate is the instance to invoke the method against and must be supplied as an object. 
    /// Subsequent parameters that are supplied as objects and are typed (with index) in argumentTypes are required.
    /// Any further parameters are typed and optional.
    /// 
    /// The target method can accept the parameters in any order. As such, types are explicit and cannot be duplicated.
    /// </summary>
    /// <typeparam name="TDelegate">Method facade</typeparam>
    /// <param name="methodInfo">Target method to invoke</param>
    /// <param name="argumentTypes">Required types (indexed 1.. in delegate)</param>
    /// <returns></returns>
    /// <example>
    ///     GenerateMethodCallToFunc<Func<object, object, IConsumerContext, CancellationToken, Task>>(methodInfo, typeof(SampleMessage));
    ///     
    ///     Initial object is the instance to invoke the method on (type determined by methodInfo.DeclaringType)
    ///     SampleMessage is required as a parameter defined by methodInfo
    ///     IConsumerContext and CancellationToken are optional parameters as defined by methodInfo. If they exist, they will be populated otherwise ignored.
    ///     
    ///     methodInfo must:
    ///         * be for an instance (static not supported in current implementation)
    ///         * contain at least a parameter of type SampleMessage
    ///         * optionally require parameters of type IConsumerContext and CancellationToken
    ///         * require no other parameters
    ///         * return a Task (as specified by the delegate)
    /// </example>
    /// <exception cref="ArgumentNullException"><see cref="methodInfo"/> is required</exception>
    /// <exception cref="ArgumentException">Target invocation requires unsupplied parameter</exception>
    /// <exception cref="ArgumentException">Required parameter(s) missing from target invocation</exception>
    public static TDelegate GenerateMethodCallToFunc<TDelegate>(MethodInfo methodInfo, params Type[] argumentTypes)
        where TDelegate : Delegate
    {
#if NETSTANDARD2_0
        if (methodInfo == null) throw new ArgumentNullException(nameof(methodInfo));
#else
        ArgumentNullException.ThrowIfNull(methodInfo);
#endif

        var delegateSignature = typeof(TDelegate).GetMethod("Invoke")!;
        Debug.Assert(delegateSignature.ReturnType == methodInfo.ReturnType);

        var instanceParameter = Expression.Parameter(typeof(object), "instance");
        var optionalTypes = delegateSignature.GetParameters()
            .Skip(argumentTypes.Length + 1)
            .Select(p => p.ParameterType);

        var parameters = argumentTypes.Select(
            (type, index) =>
                new
                {
                    Expression = Expression.Parameter(typeof(object), $"arg{index}"),
                    Required = true,
                    Type = type
                })
            .Union(
                optionalTypes.Select(
                    (type, index) =>
                        new
                        {
                            Expression = Expression.Parameter(type, $"optArg{index}"),
                            Required = false,
                            Type = type
                        }))
            .ToDictionary(x => x.Type, x => x);

        var allParameters = parameters.Select(x => x.Value.Expression).ToList();

        var argumentExpressions = methodInfo.GetParameters().Select(
            p =>
            {
                if (parameters.TryGetValue(p.ParameterType, out var arg) && parameters.Remove(p.ParameterType))
                {
                    return Expression.Convert(arg.Expression, p.ParameterType);
                }

                throw new ArgumentException($"Target invocation requires unsupplied parameter {p.ParameterType.AssemblyQualifiedName}");
            }).ToList();

        var missing = parameters.Values.Where(x => x.Required).Select(x => $"'{x.Type.AssemblyQualifiedName}'").ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"Required parameter(s) missing from target invocation ({string.Join(", ", missing)})");
        }

        var callExpression = Expression.Call(
            Expression.Convert(instanceParameter, methodInfo.DeclaringType!),
            methodInfo,
            argumentExpressions);

        var lambda = Expression.Lambda<TDelegate>(callExpression, new[] { instanceParameter }.Concat(allParameters));

        return lambda.Compile();
    }

    public static T GenerateGenericMethodCallToFunc<T>(MethodInfo genericMethod, Type[] genericTypeArguments, Type instanceType, Type returnType, params Type[] argumentTypes)
    {
        var method = genericMethod.MakeGenericMethod(genericTypeArguments);
        return GenerateMethodCallToFunc<T>(method, instanceType, returnType, argumentTypes);
    }

    private static readonly Type taskOfObject = typeof(Task<object>);
    private static readonly PropertyInfo taskOfObjectResultProperty = taskOfObject.GetProperty(nameof(Task<object>.Result));
    // Expression: TaskContinuationOptions.ExecuteSynchronously
    private static readonly ConstantExpression continuationOptionsParam = Expression.Constant(TaskContinuationOptions.ExecuteSynchronously);

    public static Func<Task<object>, Task> TaskOfObjectContinueWithTaskOfTypeFunc(Type targetType)
    {
        var taskOfType = typeof(Task<>).MakeGenericType(targetType);

        var taskOfObjectParam = Expression.Parameter(taskOfObject, "instance");
        var taskOfObjectContinueWithMethodGeneric = taskOfObject.GetMethods().First(x => x.Name == nameof(Task<object>.ContinueWith) && x.IsGenericMethodDefinition && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType == typeof(TaskContinuationOptions));
        var taskOfObjectContinueWithMethod = taskOfObjectContinueWithMethodGeneric.MakeGenericMethod(targetType);

        // Expression: x => (TargetType)x.Result
        var xParam = Expression.Parameter(taskOfObject, "x");
        var convertLambdaExpr = Expression.Lambda(Expression.Convert(Expression.Property(xParam, taskOfObjectResultProperty), targetType), xParam);

        // Expression: taskOfObject.ContinueWith(x => (TargetType)x.Result, TaskContinuationOptions.ExecuteSynchronously)
        var methodResultExpr = Expression.Call(taskOfObjectParam, taskOfObjectContinueWithMethod, convertLambdaExpr, continuationOptionsParam);
        var typedMethodResultExpr = Expression.Convert(methodResultExpr, taskOfType);

        return Expression.Lambda<Func<Task<object>, Task>>(typedMethodResultExpr, taskOfObjectParam).Compile();
    }

    static internal Func<Task, Task<object>> TaskOfTypeContinueWithTaskOfObjectFunc(Type targetType)
    {
        var taskOfType = typeof(Task<>).MakeGenericType(targetType);
        var taskOfTypeResultProperty = taskOfType.GetProperty(nameof(Task<object>.Result));

        var taskOfTypeContinueWithMethodGeneric = taskOfType.GetMethods().First(x => x.Name == nameof(Task<object>.ContinueWith) && x.IsGenericMethodDefinition && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType == typeof(TaskContinuationOptions));
        var taskOfTypeContinueWithMethod = taskOfTypeContinueWithMethodGeneric.MakeGenericMethod(typeof(object));

        // Expression: x => (TargetType)x.Result
        var xParam = Expression.Parameter(taskOfType, "x");
        var convertLambdaExpr = Expression.Lambda(Expression.Convert(Expression.Property(xParam, taskOfTypeResultProperty), typeof(object)), xParam);

        // Expression: taskOfObject.ContinueWith(x => (TargetType)x.Result, TaskContinuationOptions.ExecuteSynchronously)
        var taskParam = Expression.Parameter(typeof(Task), "task");
        var methodResultExpr = Expression.Call(Expression.Convert(taskParam, taskOfType), taskOfTypeContinueWithMethod, convertLambdaExpr, continuationOptionsParam);
        var typedMethodResultExpr = Expression.Convert(methodResultExpr, taskOfObject);

        return Expression.Lambda<Func<Task, Task<object>>>(typedMethodResultExpr, taskParam).Compile();
    }

    static internal Func<Task, object> TaskOfTypeResult(Type targetType)
    {
        var taskOfType = typeof(Task<>).MakeGenericType(targetType);
        var taskOfTypeResultProperty = taskOfType.GetProperty(nameof(Task<object>.Result));

        var taskParam = Expression.Parameter(typeof(Task), "task");

        return Expression.Lambda<Func<Task, object>>(Expression.Convert(Expression.Property(Expression.Convert(taskParam, taskOfType), taskOfTypeResultProperty), typeof(object)), taskParam).Compile();
    }
}
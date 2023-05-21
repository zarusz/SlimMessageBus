namespace SlimMessageBus.Host;

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
        var taskOfObjectContinueWithMethodGeneric = taskOfObject.GetMethods().Where(x => x.Name == nameof(Task<object>.ContinueWith) && x.IsGenericMethodDefinition && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType == typeof(TaskContinuationOptions)).First();
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

        var taskOfTypeContinueWithMethodGeneric = taskOfType.GetMethods().Where(x => x.Name == nameof(Task<object>.ContinueWith) && x.IsGenericMethodDefinition && x.GetParameters().Length == 2 && x.GetParameters()[1].ParameterType == typeof(TaskContinuationOptions)).First();
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
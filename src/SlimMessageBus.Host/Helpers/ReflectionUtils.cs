namespace SlimMessageBus.Host;

using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

public static class ReflectionUtils
{
    public static Func<object, object> GenerateGetterFunc(PropertyInfo property)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, property.DeclaringType);

        var propertyExpr = Expression.Property(typedInstanceExpr, property);
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));

        return Expression.Lambda<Func<object, object>>(propertyObjExpr, objInstanceExpr).Compile();
    }

    public static Func<object, object, object, Task> GenerateAsyncMethodCallFunc2(MethodInfo method, Type instanceType, Type argument1Type, Type argument2Type)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, instanceType);

        var objArgument1 = Expression.Parameter(typeof(object), "arg1");
        var typedArgument1Expr = Expression.TypeAs(objArgument1, argument1Type);

        var objArgument2 = Expression.Parameter(typeof(object), "arg2");
        var typedArgument2Expr = Expression.TypeAs(objArgument2, argument2Type);

        var methodResultExpr = Expression.Call(typedInstanceExpr, method, typedArgument1Expr, typedArgument2Expr);
        var methodResultAsTaskExpr = Expression.Convert(methodResultExpr, typeof(Task));

        return Expression.Lambda<Func<object, object, object, Task>>(methodResultAsTaskExpr, objInstanceExpr, objArgument1, objArgument2).Compile();
    }

    public static Func<object, object, Task> GenerateAsyncMethodCallFunc1(MethodInfo method, Type instanceType, Type argument1Type)
    {
        var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
        var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, instanceType);

        var objArgument1 = Expression.Parameter(typeof(object), "arg1");
        var typedArgument1Expr = Expression.TypeAs(objArgument1, argument1Type);

        var methodResultExpr = Expression.Call(typedInstanceExpr, method, typedArgument1Expr);
        var methodResultAsTaskExpr = Expression.Convert(methodResultExpr, typeof(Task));

        return Expression.Lambda<Func<object, object, Task>>(methodResultAsTaskExpr, objInstanceExpr, objArgument1).Compile();
    }
}
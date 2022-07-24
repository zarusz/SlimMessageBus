namespace SlimMessageBus.Host
{
    using System;
    using System.Linq;
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

        public static Func<object, object, object, Task> GenerateAsyncMethodCallLambda(MethodInfo method, Type instanceType, params Type[] argumentTypes)
        {
            var objInstanceExpr = Expression.Parameter(typeof(object), "instance");
            var typedInstanceExpr = Expression.TypeAs(objInstanceExpr, instanceType);

            var objArguments = argumentTypes.Select((x, i) => Expression.Parameter(typeof(object), $"arg{i + 1}")).ToList();
            var typedArgumentsExpr = objArguments.Select((x, i) => Expression.TypeAs(x, argumentTypes[i])).ToList();

            var methodResultExpr = Expression.Call(typedInstanceExpr, method, typedArgumentsExpr);
            var methodResultAsTaskExpr = Expression.Convert(methodResultExpr, typeof(Task));

            return Expression.Lambda<Func<object, object, object, Task>>(methodResultAsTaskExpr, new[] { objInstanceExpr }.Concat(objArguments)).Compile();
        }
    }
}
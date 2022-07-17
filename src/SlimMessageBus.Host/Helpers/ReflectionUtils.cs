namespace SlimMessageBus.Host
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public static class ReflectionUtils
    {
        public static Func<object, object> GenerateGetterLambda(PropertyInfo property)
        {
            // Define our instance parameter, which will be the input of the Func
            var objParameterExpr = Expression.Parameter(typeof(object), "instance");
            // 1. Cast the instance to the correct type
            var instanceExpr = Expression.TypeAs(objParameterExpr, property.DeclaringType);
            // 2. Call the getter and retrieve the value of the property
            var propertyExpr = Expression.Property(instanceExpr, property);
            // 3. Convert the property's value to object
            var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
            // Create a lambda expression of the latest call & compile it
            return Expression.Lambda<Func<object, object>>(propertyObjExpr, objParameterExpr).Compile();
        }
    }
}
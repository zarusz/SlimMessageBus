namespace SlimMessageBus.Host.Collections;

public class TaskOfTypeCache
{
    public Func<Task<object>, Task> FromTaskOfObject { get; }
    public Func<Task, Task<object>> ToTaskOfObject { get; }
    public Func<Task, object> GetResult { get; }

    public TaskOfTypeCache(Type type)
    {
        FromTaskOfObject = ReflectionUtils.TaskOfObjectContinueWithTaskOfTypeFunc(type);
        ToTaskOfObject = ReflectionUtils.TaskOfTypeContinueWithTaskOfObjectFunc(type);
        GetResult = ReflectionUtils.TaskOfTypeResult(type);
    }
}

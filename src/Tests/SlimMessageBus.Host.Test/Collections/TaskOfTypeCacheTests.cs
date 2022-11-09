namespace SlimMessageBus.Host.Test.Collections;

using SlimMessageBus.Host.Collections;

public class TaskOfTypeCacheTests
{
    public static IEnumerable<object[]> DataForGetResult => new List<object[]>
    {
        new object[] { typeof(object), Task.FromResult<object>(10), 10 },
        new object[] { typeof(int), Task.FromResult(10), 10 },
        new object[] { typeof(string), Task.FromResult("string"), "string" },
    };

    [Theory]
    [MemberData(nameof(DataForGetResult))]
    public async Task Given_TaskOfType_When_GetResult_Then_ReturnsResult(Type messageType, Task taskOfType, object expectedValue)
    {
        // arrange
        var subject = new TaskOfTypeCache(messageType);

        // act
        await taskOfType;
        var value = subject.GetResult(taskOfType);

        // assert
        value.Should().Be(expectedValue);
    }

    public static IEnumerable<object[]> DataFromTaskOfObject => new List<object[]>
    {
        new object[] { typeof(object), Task.FromResult<object>(10), 10 },
        new object[] { typeof(int), Task.FromResult<object>(10), 10 },
        new object[] { typeof(string), Task.FromResult<object>("string"), "string" },
    };

    [Theory]
    [MemberData(nameof(DataFromTaskOfObject))]
    public async Task Given_TaskOfObject_When_FromTaskOfObject_Then_ReturnsTaskOfType(Type messageType, Task<object> taskOfObject, object expectedValue)
    {
        // arrange
        var subject = new TaskOfTypeCache(messageType);

        // act
        var taskOfType = subject.FromTaskOfObject(taskOfObject);
        await taskOfType;

        // assert
        var value = subject.GetResult(taskOfType);
        value.Should().Be(expectedValue);
    }

    public static IEnumerable<object[]> DataToTaskOfObject => new List<object[]>
    {
        new object[] { typeof(object), Task.FromResult<object>(10), 10 },
        new object[] { typeof(int), Task.FromResult(10), 10 },
        new object[] { typeof(string), Task.FromResult("string"), "string" },
    };

    [Theory]
    [MemberData(nameof(DataToTaskOfObject))]
    public async Task Given_TaskOfType_When_FromTaskOfObject_Then_ReturnsTaskOfType(Type messageType, Task taskOfType, object expectedValue)
    {
        // arrange
        var subject = new TaskOfTypeCache(messageType);

        // act
        var taskOfObject = subject.ToTaskOfObject(taskOfType);

        // assert
        var value = await taskOfObject;
        value.Should().Be(expectedValue);
    }

}

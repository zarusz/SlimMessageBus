namespace SlimMessageBus.Host.Test;

public class ReflectionUtilsTests
{
    [Fact]
    public void When_GenerateGetterFunc_Given_TaskOfT_Then_ResultOfTaskIsObtained()
    {
        // arrange
        var taskWithResult = Task.FromResult(1);
        var resultPropertyInfo = typeof(Task<int>).GetProperty(nameof(Task<int>.Result));

        // act
        var getResultLambda = ReflectionUtils.GenerateGetterFunc(resultPropertyInfo);

        // assert
        var result = getResultLambda(taskWithResult);
        result.Should().BeOfType<int>();
        result.Should().Be(1);
    }

    [Fact]
    public async void When_GenerateMethodCallToFunc_Given_ConsumerWithOnHandlerAsyncMethodWithTwoArguments_Then_MethodIsProperlyInvoked()
    {
        // arrange
        var message = new SomeMessage();

        var instanceType = typeof(IConsumer<SomeMessage>);
        var consumerOnHandleMethodInfo = instanceType.GetMethod(nameof(IConsumer<SomeMessage>.OnHandle), new[] { typeof(SomeMessage) });

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.OnHandle(message)).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, Task>>(consumerOnHandleMethodInfo, instanceType, typeof(Task), typeof(SomeMessage));

        await callAsyncMethodFunc(consumerMock.Object, message);

        // assert
        consumerMock.Verify(x => x.OnHandle(message), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    internal record ClassWithGenericMethod(object Value)
    {
        public T GenericMethod<T>() => (T)Value;
    }

    [Fact]
    public void When_GenerateGenericMethodCallToFunc_Given_GenericMethid_Then_MethodIsProperlyInvoked()
    {
        // arrange
        var obj = new ClassWithGenericMethod(true);
        var genericMethod = typeof(ClassWithGenericMethod).GetMethods().FirstOrDefault(x => x.Name == nameof(ClassWithGenericMethod.GenericMethod));

        // act
        var methodOfTypeBoolFunc = ReflectionUtils.GenerateGenericMethodCallToFunc<Func<object, object>>(genericMethod, new[] { typeof(bool) }, obj.GetType(), typeof(object));
        var result = methodOfTypeBoolFunc(obj);

        // assert
        result.Should().Be(true);
    }

    [Fact]
    public async void When_TaskOfObjectContinueWithTaskOfTypeFunc_Given_TaskOfObject_Then_TaskTypedIsObtained()
    {
        // arrange        
        var taskOfObject = Task.FromResult<object>(10);

        // act
        var continueWithTyped = ReflectionUtils.TaskOfObjectContinueWithTaskOfTypeFunc(typeof(int));

        // assert
        var typedTask = continueWithTyped(taskOfObject);
        await typedTask;

        typedTask.GetType().Should().BeAssignableTo(typeof(Task<>).MakeGenericType(typeof(int)));

        var resultFunc = ReflectionUtils.GenerateGetterFunc(typeof(Task<int>).GetProperty(nameof(Task<int>.Result)));
        var result = resultFunc(typedTask);

        result.Should().Be(10);
    }
}
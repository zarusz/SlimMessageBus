namespace SlimMessageBus.Host.Test;

public class ReflectionUtilsTests
{
    [Fact]
    public void When_GenerateGetterFunc_Given_TaskOfT_Then_ResultOfTaskIsObtained()
    {
        // arrange
        var taskWithResult = Task.FromResult(1);
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        var resultPropertyInfo = typeof(Task<int>).GetProperty(nameof(Task<int>.Result));
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method

        // act
        var getResultLambda = ReflectionUtils.GenerateGetterFunc(resultPropertyInfo);

        // assert
        var result = getResultLambda(taskWithResult);
        result.Should().BeOfType<int>();
        result.Should().Be(1);
    }

    [Fact]
    public async Task When_GenerateMethodCallToFunc_Given_ConsumerWithOnHandlerAsyncMethodWithTwoArguments_Then_MethodIsProperlyInvoked()
    {
        // arrange
        var message = new SomeMessage();
        var cancellationToken = new CancellationToken();

        var instanceType = typeof(IConsumer<SomeMessage>);
        var consumerOnHandleMethodInfo = instanceType.GetMethod(nameof(IConsumer<SomeMessage>.OnHandle), [typeof(SomeMessage), typeof(CancellationToken)]);

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, CancellationToken, Task>>(consumerOnHandleMethodInfo);

        await callAsyncMethodFunc(consumerMock.Object, message, cancellationToken);

        // assert
        consumerMock.Verify(x => x.OnHandle(message, cancellationToken), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void When_GenerateMethodCallToFunc_Given_DelegateLessThanOneException_Then_ThrowException()
    {
        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.MethodThatHasParamatersThatCannotBeSatisfied));

        // act
        var act = () => ReflectionUtils.GenerateMethodCallToFunc<Action>(consumerHandleAMessageMethodInfo);

        // assert
        act.Should()
            .Throw<ConfigurationMessageBusException>()
            .WithMessage("Delegate * must have at least one argument");
    }

    [Fact]
    public void When_GenerateMethodCallToFunc_Given_MethodReturnTypeNotConvertableToDelegateReturnType_Then_ThrowException()
    {
        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.MethodThatHasParamatersThatCannotBeSatisfied));

        // act
        var act = () => ReflectionUtils.GenerateMethodCallToFunc<Func<object, SomeMessage, bool>>(consumerHandleAMessageMethodInfo);

        // assert
        act.Should()
            .Throw<ConfigurationMessageBusException>()
            .WithMessage("Return type mismatch for method * and delegate *");
    }

    [Fact]
    public void When_GenerateMethodCallToFunc_Given_MethodAndDelegateParamCountMismatch_Then_ThrowException()
    {
        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.MethodThatHasParamatersThatCannotBeSatisfied));

        // act
        var act = () => ReflectionUtils.GenerateMethodCallToFunc<Func<object, SomeMessage, Task>>(consumerHandleAMessageMethodInfo);

        // assert
        act.Should()
            .Throw<ConfigurationMessageBusException>()
            .WithMessage("Argument count mismatch between method * and delegate *");
    }

    internal record ClassWithGenericMethod(object Value)
    {
        public T GenericMethod<T>() => (T)Value;
    }

    [Fact]
    public void When_GenerateGenericMethodCallToFunc_Given_GenericMethod_Then_MethodIsProperlyInvoked()
    {
        // arrange
        var obj = new ClassWithGenericMethod(true);
        var genericMethod = typeof(ClassWithGenericMethod).GetMethods().FirstOrDefault(x => x.Name == nameof(ClassWithGenericMethod.GenericMethod));

        // act
        var methodOfTypeObjectFunc = ReflectionUtils.GenerateGenericMethodCallToFunc<Func<object, object>>(genericMethod, [typeof(bool)]);
        var methodOfTypeBoolFunc = ReflectionUtils.GenerateGenericMethodCallToFunc<Func<object, bool>>(genericMethod, [typeof(bool)]);

        var resultObject = methodOfTypeObjectFunc(obj);
        var resultBool = methodOfTypeBoolFunc(obj);

        // assert
        resultObject.Should().Be(true);
        resultBool.Should().Be(true);
    }

    [Fact]
    public async Task When_TaskOfObjectContinueWithTaskOfTypeFunc_Given_TaskOfObject_Then_TaskTypedIsObtained()
    {
        // arrange        
        var taskOfObject = Task.FromResult<object>(10);

        // act
        var continueWithTyped = ReflectionUtils.TaskOfObjectContinueWithTaskOfTypeFunc(typeof(int));

        // assert
        var typedTask = continueWithTyped(taskOfObject);
        await typedTask;

        typedTask.GetType().Should().BeAssignableTo(typeof(Task<>).MakeGenericType(typeof(int)));

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        var resultFunc = ReflectionUtils.GenerateGetterFunc(typeof(Task<int>).GetProperty(nameof(Task<int>.Result)));
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method
        var result = resultFunc(typedTask);

        result.Should().Be(10);
    }

    [Fact]
    public async Task When_GenerateMethodCallToFunc_Given_Delegate_Then_InstanceTypeIsInferred()
    {
        var message = new SomeMessage();
        var cancellationToken = new CancellationToken();

        var instanceType = typeof(IConsumer<SomeMessage>);
        var consumerOnHandleMethodInfo = instanceType.GetMethod(nameof(IConsumer<SomeMessage>.OnHandle), [typeof(SomeMessage), typeof(CancellationToken)]);

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.OnHandle(message, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // act (positive)
        var callAsyncMethodFunc = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, CancellationToken, Task>>(consumerOnHandleMethodInfo, typeof(SomeMessage));
        await callAsyncMethodFunc(consumerMock.Object, message, cancellationToken);

        // assert (positive)
        consumerMock.Verify(x => x.OnHandle(message, It.IsAny<CancellationToken>()), Times.Once);
        consumerMock.VerifyNoOtherCalls();

        // act (negative)
        var act = async () => await callAsyncMethodFunc(1, message, cancellationToken);

        // assertion (negative)
        await act.Should().ThrowAsync<InvalidCastException>();
    }

    [Fact]
    public async Task When_GenerateMethodCallToFunc_Given_AllOptionalParametersAreParametersOfInvocationMethod_Then_MapToInvocation()
    {
        var message = new SomeMessage();

        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.HandleAMessageWithAContext), [typeof(SomeMessage), typeof(IConsumerContext), typeof(CancellationToken)]);

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        var consumerMock = new Mock<ICustomConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.HandleAMessageWithAContext(message, consumerContextMock.Object, consumerContextMock.Object.CancellationToken)).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, IConsumerContext, CancellationToken, Task>>(consumerHandleAMessageMethodInfo, typeof(SomeMessage));

        await callAsyncMethodFunc(consumerMock.Object, message, consumerContextMock.Object, consumerContextMock.Object.CancellationToken);

        // assert
        consumerMock.Verify(x => x.HandleAMessageWithAContext(message, consumerContextMock.Object, consumerContextMock.Object.CancellationToken), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task When_GenerateMethodCallToFunc_Given_SomeOptionalParametersAreParametersOfInvocationMethod_Then_MapToInvocation()
    {
        var message = new SomeMessage();

        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.HandleAMessage), [typeof(SomeMessage), typeof(CancellationToken)]);

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        var consumerMock = new Mock<ICustomConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.HandleAMessage(message, consumerContextMock.Object.CancellationToken)).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, IConsumerContext, CancellationToken, Task>>(consumerHandleAMessageMethodInfo, typeof(SomeMessage));

        await callAsyncMethodFunc(consumerMock.Object, message, consumerContextMock.Object, consumerContextMock.Object.CancellationToken);

        // assert
        consumerMock.Verify(x => x.HandleAMessage(message, consumerContextMock.Object.CancellationToken), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void When_GenerateMethodCallToFunc_Given_InvocationMethodCannotBeSatisfied_Then_ThrowException()
    {
        var message = new SomeMessage();

        var instanceType = typeof(ICustomConsumer<SomeMessage>);
        var consumerHandleAMessageMethodInfo = instanceType.GetMethod(nameof(ICustomConsumer<SomeMessage>.MethodThatHasParamatersThatCannotBeSatisfied));

        var consumerContextMock = new Mock<IConsumerContext>();
        consumerContextMock.SetupGet(x => x.CancellationToken).Returns(new CancellationToken());

        // act
        var act = () => ReflectionUtils.GenerateMethodCallToFunc<Func<object, object, IConsumerContext, CancellationToken, Task>>(consumerHandleAMessageMethodInfo, typeof(SomeMessage));

        // assert
        act.Should().Throw<ArgumentException>();
    }
}
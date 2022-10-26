namespace SlimMessageBus.Host.Test;

public class ReflectionUtilsTests
{
    [Fact]
    public void When_GenerateGetterFunc_Given_TaskOfT_Then_ResultOfTaskIsObtained()
    {
        // arrange
        Task<int> taskWithResult = Task.FromResult(1);
        var resultPropertyInfo = typeof(Task<int>).GetProperty(nameof(Task<int>.Result));

        // act
        var getResultLambda = ReflectionUtils.GenerateGetterFunc(resultPropertyInfo);

        // assert
        var result = getResultLambda(taskWithResult);
        result.Should().BeOfType<int>();
        result.Should().Be(1);
    }

    [Fact]
    public async void When_GenerateAsyncMethodCallLambda_Given_ConsumerWithOnHandlerAsyncMethodWithTwoArguments_Then_MethodIsProperlyInvoked()
    {
        // arrange
        var message = new SomeMessage();

        var instanceType = typeof(IConsumer<SomeMessage>);
        var consumerOnHandleMethodInfo = instanceType.GetMethod(nameof(IConsumer<SomeMessage>.OnHandle), new[] { typeof(SomeMessage) });

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.OnHandle(message)).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateAsyncMethodCallFunc1(consumerOnHandleMethodInfo, instanceType, typeof(SomeMessage));

        await callAsyncMethodFunc(consumerMock.Object, message);

        // assert
        consumerMock.Verify(x => x.OnHandle(message), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }
}
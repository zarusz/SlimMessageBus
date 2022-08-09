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
        var path = "some-path";

        var instanceType = typeof(IConsumer<SomeMessage>);
        var consumerOnHandleMethodInfo = instanceType.GetMethod(nameof(IConsumer<SomeMessage>.OnHandle), new[] { typeof(SomeMessage), typeof(string) });

        var consumerMock = new Mock<IConsumer<SomeMessage>>();
        consumerMock.Setup(x => x.OnHandle(message, path)).Returns(Task.CompletedTask);

        // act
        var callAsyncMethodFunc = ReflectionUtils.GenerateAsyncMethodCallFunc2(consumerOnHandleMethodInfo, instanceType, typeof(SomeMessage), typeof(string));
        
        await callAsyncMethodFunc(consumerMock.Object, message, path);

        // assert
        consumerMock.Verify(x => x.OnHandle(message, path), Times.Once);
        consumerMock.VerifyNoOtherCalls();
    }
}
namespace SlimMessageBus.Host.Test
{
    using FluentAssertions;
    using System.Threading.Tasks;
    using Xunit;

    public class ReflectionUtilsTests
    {
        [Fact]
        public void Given_TaskOfT_When_ResultIsObtainedWithAnCompiledExpression_Then_ValueIsObtained()
        {
            // arrange
            Task<int> taskWithResult = Task.FromResult(1);
            var resultPropertyInfo = typeof(Task<int>).GetProperty(nameof(Task<int>.Result));

            // act
            var getResultLambda = ReflectionUtils.GenerateGetterLambda(resultPropertyInfo);

            // assert
            var result = getResultLambda(taskWithResult);
            result.Should().BeOfType<int>();
            result.Should().Be(1);
        }
    }
}
namespace SlimMessageBus.Host.Outbox.Test;

public static class IEnumerableExtensionsTests
{
    public class BatchTests
    {
        [Fact]
        public void Batch_NullInput_ThrowsArgumentNullException()
        {
            // Arrange
            IEnumerable<int> items = null;
            var batchSize = 2;

            // Act
            var act = () => items.Batch(batchSize).ToList();

            // Assert
            act.Should().Throw<ArgumentNullException>().WithMessage("Value cannot be null. (Parameter 'items')");
        }

        [Fact]
        public void Batch_BatchSizeZero_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var items = Enumerable.Range(1, 10);
            var batchSize = 0;

            // Act
            var act = () => items.Batch(batchSize).ToList();

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Batch size must be greater than zero. (Parameter 'batchSize')");
        }

        [Fact]
        public void Batch_BatchSizeNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var items = Enumerable.Range(1, 10);
            var batchSize = -1;

            // Act
            var act = () => items.Batch(batchSize).ToList();

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("Batch size must be greater than zero. (Parameter 'batchSize')");
        }

        [Fact]
        public void Batch_BatchSizeGreaterThanCollection_ReturnsSingleBatch()
        {
            // Arrange
            var items = Enumerable.Range(1, 5);
            var batchSize = 10;

            // Act
            var result = items.Batch(batchSize).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].Should().BeEquivalentTo(new List<int> { 1, 2, 3, 4, 5 });
        }

        [Fact]
        public void Batch_ValidBatchSize_ReturnsCorrectBatches()
        {
            // Arrange
            var items = Enumerable.Range(1, 10);
            var batchSize = 3;

            // Act
            var result = items.Batch(batchSize).ToList();

            // Assert
            result.Should().HaveCount(4);
            result[0].Should().BeEquivalentTo(new List<int> { 1, 2, 3 });
            result[1].Should().BeEquivalentTo(new List<int> { 4, 5, 6 });
            result[2].Should().BeEquivalentTo(new List<int> { 7, 8, 9 });
            result[3].Should().BeEquivalentTo(new List<int> { 10 });
        }

        [Fact]
        public void Batch_EmptyCollection_ReturnsNoBatches()
        {
            // Arrange
            var items = Enumerable.Empty<int>();
            var batchSize = 3;

            // Act
            var result = items.Batch(batchSize).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void Batch_BatchSizeOne_ReturnsSingleItemBatches()
        {
            // Arrange
            var items = Enumerable.Range(1, 5);
            var batchSize = 1;

            // Act
            var result = items.Batch(batchSize).ToList();

            // Assert
            result.Should().HaveCount(5);
            result[0].Should().BeEquivalentTo(new List<int> { 1 });
            result[1].Should().BeEquivalentTo(new List<int> { 2 });
            result[2].Should().BeEquivalentTo(new List<int> { 3 });
            result[3].Should().BeEquivalentTo(new List<int> { 4 });
            result[4].Should().BeEquivalentTo(new List<int> { 5 });
        }
    }
}
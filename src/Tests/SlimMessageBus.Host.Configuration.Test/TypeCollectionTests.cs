namespace SlimMessageBus.Host.Configuration.Test;

public class TypeCollectionTests
{
    [Fact]
    public void Add_Should_AddTypeToCollection_IfAssignableToGeneric()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        collection.Add(typeof(SampleClass));

        // Assert
        collection.Count.Should().Be(1);
        collection.Should().Contain(typeof(SampleClass));
    }

    [Fact]
    public void Add_Should_ThrowException_IfNotAssignableToGeneric()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        var act = () => collection.Add(typeof(object));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage($"Type is not assignable to '{typeof(ISampleInterface)}'. (Parameter 'type')");
    }

    [Fact]
    public void Add_Should_ThrowException_WhenTypeIsAssignableToGenericButAlreadyExists()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        Action act = () => collection.Add(typeof(SampleClass));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Type already exists in the collection. (Parameter 'type')");
    }

    [Fact]
    public void Add_Should_AddTypeToCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        collection.Add<SampleClass>();

        // Assert
        collection.Count.Should().Be(1);
        collection.Should().Contain(typeof(SampleClass));
    }

    [Fact]
    public void Add_Should_ThrowException_WhenTypeAlreadyExists()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        Action act = () => collection.Add<SampleClass>();

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Type already exists in the collection. (Parameter 'type')");
    }

    [Fact]
    public void TryAdd_Should_AddTypeToCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        var result = collection.TryAdd<SampleClass>();

        // Assert
        result.Should().BeTrue();
        collection.Count.Should().Be(1);
        collection.Should().Contain(typeof(SampleClass));
    }

    [Fact]
    public void TryAdd_Should_ReturnFalse_WhenTypeAlreadyExists()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        var result = collection.TryAdd<SampleClass>();

        // Assert
        result.Should().BeFalse();
        collection.Count.Should().Be(1);
    }

    [Fact]
    public void Clear_Should_RemoveAllTypesFromCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();
        collection.Add<AnotherSampleClass>();

        // Act
        collection.Clear();

        // Assert
        collection.Count.Should().Be(0);
        collection.Should().NotContain(typeof(SampleClass));
        collection.Should().NotContain(typeof(AnotherSampleClass));
    }

    [Fact]
    public void Contains_Should_ReturnTrue_WhenTypeExistsInCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        var contains = collection.Contains<SampleClass>();

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void Contains_Should_ReturnFalse_WhenTypeDoesNotExistInCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        var contains = collection.Contains<SampleClass>();

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void Remove_Should_RemoveTypeFromCollection_WhenSuppliedAsGenericParameter()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        var removed = collection.Remove<SampleClass>();

        // Assert
        removed.Should().BeTrue();
        collection.Count.Should().Be(0);
        collection.Should().NotContain(typeof(SampleClass));
    }

    [Fact]
    public void Remove_Should_RemoveTypeFromCollection_WhenSuppliedAsType()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();

        // Act
        var removed = collection.Remove(typeof(SampleClass));

        // Assert
        removed.Should().BeTrue();
        collection.Count.Should().Be(0);
        collection.Should().NotContain(typeof(SampleClass));
    }

    [Fact]
    public void Remove_Should_ReturnFalse_WhenTypeDoesNotExistInCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();

        // Act
        var removed = collection.Remove<SampleClass>();

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Enumerator_Should_IterateOverCollection()
    {
        // Arrange
        var collection = new TypeCollection<ISampleInterface>();
        collection.Add<SampleClass>();
        collection.Add<AnotherSampleClass>();

        // Act
        var types = new List<Type>();
        foreach (var type in collection)
        {
            types.Add(type);
        }

        // Assert
        types.Should().ContainInOrder(typeof(SampleClass), typeof(AnotherSampleClass));
    }

    public interface ISampleInterface { }

    public class SampleClass : ISampleInterface { }

    public class AnotherSampleClass : ISampleInterface { }
}

namespace FlexiMap.Test;

public class ObjectMapperTest
{
    [Fact]
    public void Map_SimpleProperties_ShouldMapCorrectly()
    {
        // Arrange
        var source = new Source { Id = 1, Name = "Test" };

        // Act
        var destination = source.Map<Destination>();

        // Assert
        Assert.Equal(source.Id, destination.Id);
        Assert.Equal(source.Name, destination.Name);
    }

    [Fact]
    public void Map_NestedObjects_ShouldMapCorrectly()
    {
        // Arrange
        var source = new Source { Id = 1, Nested = new Nested { Value = "NestedValue" } };

        // Act
        var destination = source.Map<Destination>();

        // Assert
        Assert.Equal(source.Nested.Value, destination.Nested.Value);
    }

    [Fact]
    public void Map_Collection_ShouldMapCorrectly()
    {
        // Arrange
        var sourceList = new List<Source> { new Source { Id = 1 }, new Source { Id = 2 } };

        // Act
        var destinationList = sourceList.MapCollection<Destination>();

        // Assert
        Assert.Equal(sourceList.Count, destinationList.Count);
        for (int i = 0; i < sourceList.Count; i++)
        {
            Assert.Equal(sourceList[i].Id, destinationList[i].Id);
        }
    }

    [Fact]
    public async Task MapAsync_CustomMapping_ShouldApplyCorrectly()
    {
        // Arrange
        var source = new Source { Id = 1, Name = "AsyncTest" };

        // Act
        var destination = await source.MapAsync<Destination>(async d =>
        {
            // Custom async mapping logic
            d.Name = await Task.FromResult(source.Name + " - Async");
        });

        // Assert
        Assert.Equal(source.Name + " - Async", destination.Name);
    }

    [Fact]
    public void Map_NullSafety_ShouldHandleNullValuesGracefully()
    {
        // Arrange
        var source = new Source { Id = 1, Name = null };

        // Act
        var destination = source.Map<Destination>();

        // Assert
        Assert.Null(destination.Name);
    }

    [Fact]
    public void Map_CustomTransformation_ShouldApplyCorrectly()
    {
        // Arrange
        var source = new Source { Id = 1, Name = "Test" };

        // Act
        var destination = source.Map<Destination>(d => d.Name = "CustomName");

        // Assert
        Assert.Equal("CustomName", destination.Name);
    }

    [Fact]
    public async Task MapAsync_CollectionAsync_ShouldMapCorrectly()
    {
        // Arrange
        var sourceList = new List<Source> { new Source { Id = 1 }, new Source { Id = 2 } };

        // Act
        var destinationList = await sourceList.MapCollectionAsync<Destination>(async d =>
        {
            // Custom async mapping logic
            await Task.CompletedTask;
        });

        // Assert
        Assert.Equal(sourceList.Count, destinationList.Count);
        for (int i = 0; i < sourceList.Count; i++)
        {
            Assert.Equal(sourceList[i].Id, destinationList[i].Id);
        }
    }

    [Fact]
    public void Map_TypeSafety_ShouldThrowForMismatchedTypes()
    {
        // Arrange
        var source = new Source { Id = 1, Name = "Test" };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => source.Map<InvalidDestination>());
    }

    [Fact]
    public void Map_Performance_ShouldBeWithinExpectedLimits()
    {
        // Arrange
        var largeSourceList = new List<Source>();
        for (int i = 0; i < 1000; i++)
        {
            largeSourceList.Add(new Source { Id = i });
        }

        // Act
        var startTime = DateTime.Now;
        var largeDestinationList = largeSourceList.MapCollection<Destination>();
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        // Assert
        Assert.True(duration < 1000, "Mapping took longer than expected");
    }

    [Fact]
    public void Map_ShouldMapPublicAndPrivateProperties()
    {
        // Arrange
        var source = new PrivateSourceClass(10, "secret");

        // Act
        var destination = source.Map<PrivateDestinationClass>();

        // Assert
        Assert.Equal(source.PublicProperty, destination.PublicProperty);
        Assert.Equal(source.GetType().GetProperty("PrivateProperty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                               .GetValue(source), destination.GetType().GetProperty("PrivateProperty").GetValue(destination));
    }

    [Fact]
    public void Map_CircularReferenceInCollection_ShouldHandleGracefully()
    {
        // Arrange
        var circularSource = new CircularSource
        {
            Name = "Root",
            Children = new List<CircularSource>
        {
            new CircularSource { Name = "Child1" },
            new CircularSource { Name = "Child2" }
        }
        };
        circularSource.Children.Add(circularSource); // Circular reference

        // Act
        var result = circularSource.Map<CircularDestination>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Root", result.Name);
        Assert.NotNull(result.Children);
        Assert.Equal(3, result.Children.Count);
        Assert.Null(result.Children[2]); // Circular reference should map to null
    }
}

public class Source
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Nested Nested { get; set; }
}

public class Destination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Nested Nested { get; set; }
}

public class Nested
{
    public string Value { get; set; }
}

public class InvalidDestination
{
    public DateTime Id { get; set; } // Different type from Source.Id
    public string Name { get; set; }
}

public class PrivateSourceClass
{
    public int PublicProperty { get; set; }
    private string PrivateProperty { get; set; }

    public PrivateSourceClass(int publicValue, string privateValue)
    {
        PublicProperty = publicValue;
        PrivateProperty = privateValue;
    }
}

public class PrivateDestinationClass
{
    public int PublicProperty { get; set; }
    public string PrivateProperty { get; set; }
}

public class CircularSource
{
    public string Name { get; set; }
    public List<CircularSource> Children { get; set; } = new List<CircularSource>();
}


public class CircularDestination
{
    public string Name { get; set; }
    public List<CircularDestination> Children { get; set; } = new List<CircularDestination>();
}
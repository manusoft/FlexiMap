using System.Collections;

namespace FlexiMap.Test;

public class MappingTests
{

    public class Source
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Source Nested { get; set; }
        public List<int> Numbers { get; set; }
    }

    // Redefine Destination as a record
    public record Destination
    {
        public int Id { get; init; }
        public string Name { get; set; } // Mutable for test purposes
        public Destination Nested { get; init; }
        public int[] Numbers { get; init; }

        // Optional: Add a parameterless constructor for Activator.CreateInstance
        public Destination() { }
    }


    [Fact]
    public void Map_WithCustomMapping_AppliesCustomLogic()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name");
        var source = new Source { Id = 1, Name = "Test" };
        Destination destination = null;
        source.Map<Destination>(d => destination = d with { Name = "Custom" }, config: config);

        Assert.Equal("Custom", destination.Name);
        Assert.Equal(source.Id, destination.Id);
    }

    [Fact]
    public async Task MapAsync_WithCustomMapping_AppliesCustomLogic()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name");
        var source = new Source { Id = 1, Name = "Test" };
        Destination destination = null;
        await source.MapAsync<Destination>(async d => { destination = d with { Name = "Custom" }; await Task.CompletedTask; }, config: config);

        Assert.Equal("Custom", destination.Name);
        Assert.Equal(source.Id, destination.Id);
    }

    [Fact]
    public void Map_WithDefaultConfig_MapsPropertiesCorrectly()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name")
            .MapProperty("Numbers", "Numbers");
        var source = new Source { Id = 1, Name = "Test", Numbers = new List<int> { 1, 2, 3 } };
        var destination = source.Map<Destination>(config: config);

        Assert.Equal(source.Id, destination.Id);
        Assert.Equal(source.Name, destination.Name);
        Assert.Null(destination.Nested); // No nested mapping by default
        Assert.Equal(source.Numbers.ToArray(), destination.Numbers);
    }

    [Fact]
    public void Map_WithCircularReferences_HandlesCircularity()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name");
        var source = new Source { Id = 1, Name = "Test" };
        source.Nested = source; // Circular reference
        var destination = source.Map<Destination>(config: config, handleCircularReferences: true);

        Assert.Equal(source.Id, destination.Id);
        Assert.Null(destination.Nested); // Circular reference should be broken
    }

    [Fact]
    public void Map_WithNullSource_ThrowsArgumentNullException()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id");
        Source source = null;
        Assert.Throws<ArgumentNullException>(() => source.Map<Destination>(config: config));
    }

    [Fact]
    public async Task MapAsync_WithDefaultConfig_MapsPropertiesCorrectly()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name")
            .MapProperty("Numbers", "Numbers");
        var source = new Source { Id = 1, Name = "Test", Numbers = new List<int> { 1, 2, 3 } };
        var destination = await source.MapAsync<Destination>(config: config);

        Assert.Equal(source.Id, destination.Id);
        Assert.Equal(source.Name, destination.Name);
        Assert.Null(destination.Nested);
        Assert.Equal(source.Numbers.ToArray(), destination.Numbers);
    }

    [Fact]
    public async Task MapAsync_WithNullSource_ThrowsArgumentNullException()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id");
        Source source = null;
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await source.MapAsync<Destination>(config: config));
    }

    [Fact]
    public void MapCollection_WithDefaultConfig_MapsCollectionCorrectly()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name")
            .MapProperty("Numbers", "Numbers");
        var source = new[] { new Source { Id = 1, Name = "A" }, new Source { Id = 2, Name = "B" } };
        var destination = source.MapCollection<Destination>(config: config);

        Assert.Equal(source.Length, destination.Count);
        Assert.Equal(source[0].Id, destination[0].Id);
        Assert.Equal(source[0].Name, destination[0].Name);
        Assert.Equal(source[1].Id, destination[1].Id);
        Assert.Equal(source[1].Name, destination[1].Name);
    }

    [Fact]
    public void MapCollection_WithNullSource_ThrowsArgumentNullException()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id");
        IEnumerable source = null;
        Assert.Throws<ArgumentNullException>(() => source.MapCollection<Destination>(config: config));
    }

    [Fact]
    public async Task MapCollectionAsync_WithDefaultConfig_MapsCollectionCorrectly()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name")
            .MapProperty("Numbers", "Numbers");
        var source = new[] { new Source { Id = 1, Name = "A" }, new Source { Id = 2, Name = "B" } };
        var destination = await source.MapCollectionAsync<Destination>(config: config);

        Assert.Equal(source.Length, destination.Count);
        Assert.Equal(source[0].Id, destination[0].Id);
        Assert.Equal(source[0].Name, destination[0].Name);
        Assert.Equal(source[1].Id, destination[1].Id);
        Assert.Equal(source[1].Name, destination[1].Name);
    }

    [Fact]
    public async Task MapCollectionAsync_WithCustomMapping_AppliesCustomLogic()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id")
            .MapProperty("Name", "Name");
        var source = new[] { new Source { Id = 1, Name = "A" } };
        var destination = await source.MapCollectionAsync<Destination>(async d => { d.Name = "Custom"; await Task.CompletedTask; }, config: config);

        Assert.Equal("Custom", destination[0].Name);
        Assert.Equal(source[0].Id, destination[0].Id);
    }

    [Fact]
    public async Task MapCollectionAsync_WithNullSource_ThrowsArgumentNullException()
    {
        var config = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .MapProperty("Id", "Id");
        IEnumerable source = null;
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await source.MapCollectionAsync<Destination>(config: config));
    }

    [Fact]
    public void MappingConfiguration_ForTypes_SetsCurrentTypePair()
    {
        var config = new MappingConfiguration();
        var result = config.ForTypes<Source, Destination>();

        Assert.NotNull(result._currentTypePair);
        Assert.Equal(typeof(Source), result._currentTypePair.Value.Source);
        Assert.Equal(typeof(Destination), result._currentTypePair.Value.Destination);
    }

    [Fact]
    public void MappingConfiguration_MapProperty_MapsPropertyCorrectly()
    {
        var config = new MappingConfiguration();
        var result = config.ForTypes<Source, Destination>()
            .MapProperty("Id", "Id");

        var mappings = result.PropertyMappings[(typeof(Source), typeof(Destination))];
        Assert.True(mappings.ContainsKey("Id"));
        Assert.Equal("Id", mappings["Id"].DestProp);
    }

    [Fact]
    public void MappingConfiguration_Validate_WithPriorityConflict_ThrowsException()
    {
        var config = new MappingConfiguration();
        config.ForTypes<Source, Destination>()
            .MapProperty("Id", "Id", 1)
            .MapProperty("Name", "Id", 1);

        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void MappingConfiguration_ExcludeProperty_ExcludesProperty()
    {
        var config = new MappingConfiguration();
        var result = config.ForTypes<Source, Destination>()
            .ExcludeProperty("Id");

        Assert.Contains((typeof(Source), typeof(Destination), "Id"), result.ExcludedProperties);
    }

    [Fact]
    public void MappingConfiguration_SetDefaultValue_SetsDefault()
    {
        var config = new MappingConfiguration();
        var result = config.ForTypes<Source, Destination>()
            .SetDefaultValue("Id", 0);

        Assert.True(result.DefaultValues.ContainsKey((typeof(Source), typeof(Destination), "Id")));
        Assert.Equal(0, result.DefaultValues[(typeof(Source), typeof(Destination), "Id")]);
    }

    [Fact]
    public void MappingConfiguration_AddConverterFallback_RegistersFallback()
    {
        var config = new MappingConfiguration();
        var result = config.ForTypes<Source, Destination>()
            .AddConverterFallback<int, string>(x => x.ToString());

        var fallback = result.GetConverterFallback(typeof(Source), typeof(Destination), typeof(int), typeof(string));
        Assert.NotNull(fallback);
    }
}

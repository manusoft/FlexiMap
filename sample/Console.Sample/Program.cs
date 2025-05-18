using FlexiMap;

// Scenario 1: Without configuration (automatic mapping)
var source1 = new Source
{
Id = 1,
Name = "Source Name (Auto)",
Description = "Source Description (Auto)"
};

var destination1 = source1.Map<Destination>(); // No config, uses automatic mapping

Console.WriteLine("Without Configuration:");
Console.WriteLine($"Name: {destination1.Name}");
Console.WriteLine($"Description: {destination1.Description}");

// Scenario 2: With configuration (explicit mapping)
var config = new MappingConfiguration()
    .ForTypes<Source, Destination>()
    .MapProperty("Name", "Name")
    .MapProperty("Description", "Description");

var source2 = new Source
{
Id = 2,
Name = "Source Name (Config)",
Description = "Source Description (Config)"
};

var destination2 = source2.Map<Destination>(config: config); // With config, uses explicit mapping

Console.WriteLine("\nWith Configuration:");
Console.WriteLine($"Name: {destination2.Name}");
Console.WriteLine($"Description: {destination2.Description}");

// Source class
public class Source
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

// Destination class
public class Destination
{
    public string Name { get; set; }
    public string Description { get; set; }
}
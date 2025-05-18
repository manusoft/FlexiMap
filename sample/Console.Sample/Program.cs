// See https://aka.ms/new-console-template for more information
using FlexiMap;

Console.WriteLine("Hello, World!");

var mappingConfig = new MappingConfiguration()
            .ForTypes<Source, Destination>()
            .ExcludeProperty("Id") // Exclude Id property from mapping
            .MapProperty("Name", "Name")
            .MapProperty("Description", "Description");

var source = new Source
{
    Id = 1,
    Name = "Source Name",
    Description = "Source Description"
};

var destination = source.Map<Destination>(config: mappingConfig);

Console.WriteLine($"Name: {destination.Name}");
Console.WriteLine($"Description: {destination.Description}");

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
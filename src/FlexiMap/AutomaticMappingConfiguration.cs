namespace FlexiMap;

//public class AutomaticMappingConfiguration
//{
//    public bool CaseInsensitive { get; private set; }
//    public HashSet<string> ExcludedProperties { get; } = new HashSet<string>();

//    public AutomaticMappingConfiguration ConfigureCaseSensitivity(bool caseInsensitive)
//    {
//        CaseInsensitive = caseInsensitive;
//        return this;
//    }

//    public AutomaticMappingConfiguration ExcludeProperty(string propertyName)
//    {
//        ExcludedProperties.Add(propertyName);
//        return this;
//    }
//}

[AttributeUsage(AttributeTargets.Property)]
public class MapToAttribute : Attribute
{
    public string DestinationProperty { get; }
    public MapToAttribute(string destinationProperty)
    {
        DestinationProperty = destinationProperty;
    }
}
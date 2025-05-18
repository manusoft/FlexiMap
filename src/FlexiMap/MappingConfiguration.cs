namespace FlexiMap;

public class MappingConfiguration
{
    private readonly Dictionary<(Type Source, Type Destination), Dictionary<string, string>> _propertyMappings = new();
    private readonly HashSet<(Type Source, Type Destination, string Property)> _excludedProperties = new();
    private (Type Source, Type Destination)? _currentTypePair;

    public Dictionary<(Type Source, Type Destination), Dictionary<string, string>> PropertyMappings => _propertyMappings;
    public HashSet<(Type Source, Type Destination, string Property)> ExcludedProperties => _excludedProperties;

    // Fluent API: Start configuration for a source and destination type pair
    public MappingConfiguration ForTypes<TSource, TDestination>()
    {
        _currentTypePair = (typeof(TSource), typeof(TDestination));
        return this;
    }

    // Fluent API: Map a source property to a destination property
    public MappingConfiguration MapProperty(string sourceProperty, string destinationProperty)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before mapping properties.");

        var (sourceType, destType) = _currentTypePair.Value;
        if (!_propertyMappings.ContainsKey((sourceType, destType)))
            _propertyMappings[(sourceType, destType)] = new Dictionary<string, string>();

        _propertyMappings[(sourceType, destType)][sourceProperty] = destinationProperty;
        return this;
    }

    // Fluent API: Exclude a property from mapping
    public MappingConfiguration ExcludeProperty(string propertyName)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before excluding properties.");

        var (sourceType, destType) = _currentTypePair.Value;
        _excludedProperties.Add((sourceType, destType, propertyName));
        return this;
    }

    // Fluent API: Reset the current type pair for new configurations
    public MappingConfiguration Reset()
    {
        _currentTypePair = null;
        return this;
    }

    // Non-fluent methods for backward compatibility
    public void AddPropertyMapping(Type sourceType, Type destType, string sourceProp, string destProp)
    {
        var key = (sourceType, destType);
        if (!_propertyMappings.ContainsKey(key))
            _propertyMappings[key] = new Dictionary<string, string>();
        _propertyMappings[key][sourceProp] = destProp;
    }

    public void ExcludeProperty(Type sourceType, Type destType, string propertyName)
    {
        _excludedProperties.Add((sourceType, destType, propertyName));
    }
}

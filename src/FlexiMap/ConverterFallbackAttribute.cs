namespace FlexiMap;

// Custom attribute for dynamic converter fallbacks
[AttributeUsage(AttributeTargets.Method)]
public class ConverterFallbackAttribute : Attribute
{
    public Type SourceType { get; }
    public Type DestinationType { get; }

    public ConverterFallbackAttribute(Type sourceType, Type destinationType)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
    }
}
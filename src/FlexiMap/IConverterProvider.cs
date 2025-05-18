namespace FlexiMap;

// Interface for dynamic converter providers
public interface IConverterProvider
{
    IEnumerable<(Type SourceType, Type DestType, Func<object, Task<object>> Converter)> GetConverters();
}
namespace FlexiMap;

// Example converter provider
public class CustomConverterProvider : IConverterProvider
{
    public IEnumerable<(Type SourceType, Type DestType, Func<object, Task<object>> Converter)> GetConverters()
    {
        yield return (typeof(bool), typeof(string), x => Task.FromResult<object>(((bool)x).ToString()));
        yield return (typeof(string), typeof(bool), x => Task.FromResult<object>(bool.TryParse((string)x, out var result) ? result : false));
    }
}
using System.Collections.Concurrent;
using System.Reflection;

namespace FlexiMap;

// Utility class for shared methods
public static class MappingUtils
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    public static PropertyInfo[] GetCachedProperties(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        return _propertyCache.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic));
    }

    public static bool IsSimpleType(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
               type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }
}
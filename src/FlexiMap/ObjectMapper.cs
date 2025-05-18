using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace FlexiMap;

public static class ObjectMapper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
    private static readonly MappingConfiguration _defaultConfig = new();

    public static TDestination Map<TDestination>(
        this object source,
        Action<TDestination> customMapping = null,
        MappingConfiguration config = null,
        bool handleCircularReferences = true)
        where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();
        MapProperties(source, destination, config ?? _defaultConfig, handleCircularReferences ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null);
        customMapping?.Invoke(destination);
        return destination;
    }

    public static async Task<TDestination> MapAsync<TDestination>(
        this object source,
        Func<TDestination, Task> customMapping = null,
        MappingConfiguration config = null,
        bool handleCircularReferences = true)
        where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();
        MapProperties(source, destination, config ?? _defaultConfig, handleCircularReferences ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null);
        if (customMapping != null) await customMapping(destination);
        return destination;
    }

    public static List<TDestination> MapCollection<TDestination>(
        this IEnumerable source,
        MappingConfiguration config = null)
        where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destinationList = new List<TDestination>();
        foreach (var item in source)
        {
            destinationList.Add(item.Map<TDestination>(config: config));
        }
        return destinationList;
    }

    public static async Task<List<TDestination>> MapCollectionAsync<TDestination>(
        this IEnumerable source,
        Func<TDestination, Task> customMapping = null,
        MappingConfiguration config = null)
        where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destinationList = new List<TDestination>();
        foreach (var item in source)
        {
            destinationList.Add(await item.MapAsync(customMapping, config));
        }
        return destinationList;
    }

    private static void MapProperties(
        object source,
        object destination,
        MappingConfiguration config,
        HashSet<object> visited)
    {
        if (source == null || destination == null) return;
        if (visited?.Contains(source) == true) return;

        visited?.Add(source);

        var sourceType = source.GetType();
        var destType = destination.GetType();
        var sourceProps = GetCachedProperties(sourceType);
        var destProps = GetCachedProperties(destType).ToDictionary(p => p.Name, p => p);
        var mappings = config.PropertyMappings.GetValueOrDefault((sourceType, destType)) ?? new Dictionary<string, string>();

        foreach (var sourceProp in sourceProps)
        {
            var sourcePropName = sourceProp.Name;
            if (config.ExcludedProperties.Contains((sourceType, destType, sourcePropName))) continue;

            var destPropName = mappings.GetValueOrDefault(sourcePropName, sourcePropName);
            if (!destProps.TryGetValue(destPropName, out var destProp) || !destProp.CanWrite) continue;

            var sourceValue = sourceProp.GetValue(source);
            if (sourceValue == null)
            {
                destProp.SetValue(destination, null);
                continue;
            }

            try
            {
                MapProperty(sourceProp, destProp, sourceValue, destination, config, visited);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to map property '{sourcePropName}' from {sourceType.Name} to {destType.Name}: {ex.Message}",
                    ex);
            }
        }

        visited?.Remove(source);
    }

    private static void MapProperty(
        PropertyInfo sourceProp,
        PropertyInfo destProp,
        object sourceValue,
        object destination,
        MappingConfiguration config,
        HashSet<object> visited)
    {
        if (IsSimpleType(destProp.PropertyType))
        {
            if (!sourceProp.PropertyType.IsAssignableTo(destProp.PropertyType))
            {
                throw new InvalidOperationException(
                    $"Type mismatch: cannot map from {sourceProp.PropertyType.Name} to {destProp.PropertyType.Name}");
            }
            destProp.SetValue(destination, sourceValue);
        }
        else if (typeof(IEnumerable).IsAssignableFrom(destProp.PropertyType) && destProp.PropertyType != typeof(string))
        {
            var collection = MapCollection(sourceValue as IEnumerable, destProp.PropertyType, config, visited);
            destProp.SetValue(destination, collection);
        }
        else
        {
            var nestedObject = Activator.CreateInstance(destProp.PropertyType)
                ?? throw new InvalidOperationException($"Cannot create instance of {destProp.PropertyType.Name}");
            MapProperties(sourceValue, nestedObject, config, visited);
            destProp.SetValue(destination, nestedObject);
        }
    }

    private static object MapCollection(
        IEnumerable source,
        Type destinationType,
        MappingConfiguration config,
        HashSet<object> visited)
    {
        if (source == null) return null;

        var itemType = destinationType.IsGenericType
            ? destinationType.GetGenericArguments()[0]
            : destinationType.GetElementType() ?? typeof(object);

        var listType = typeof(List<>).MakeGenericType(itemType);
        var destinationList = (IList)Activator.CreateInstance(listType)
            ?? throw new InvalidOperationException($"Cannot create collection of type {listType.Name}");

        foreach (var item in source)
        {
            if (item == null || visited?.Contains(item) == true)
            {
                destinationList.Add(null);
                continue;
            }

            visited?.Add(item);
            var mappedItem = Activator.CreateInstance(itemType)
                ?? throw new InvalidOperationException($"Cannot create instance of {itemType.Name}");
            MapProperties(item, mappedItem, config, visited);
            destinationList.Add(mappedItem);
        }

        if (destinationType.IsArray)
        {
            var array = Array.CreateInstance(itemType, destinationList.Count);
            destinationList.CopyTo(array, 0);
            return array;
        }

        return destinationList;
    }

    private static PropertyInfo[] GetCachedProperties(Type type)
    {
        return _propertyCache.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic));
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive || type.IsValueType || type == typeof(string) || type == typeof(DateTime);
    }
}
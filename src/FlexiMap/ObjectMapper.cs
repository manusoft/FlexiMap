using System.Collections;
using System.Reflection;

namespace FlexiMap;

public static class ObjectMapper
{
    private static readonly MappingConfiguration _defaultConfig = new();
    private static bool _caseInsensitiveAutomaticMapping = false;

    public static void ConfigureAutomaticMapping(bool caseInsensitive = false)
    {
        _caseInsensitiveAutomaticMapping = caseInsensitive;
    }

    static ObjectMapper()
    {
        _defaultConfig.ForTypes<object, object>()
            .AddConverterFallback<int, string>(x => x.ToString())
            .AddConverterFallback<double, string>(x => x.ToString("F2"))
            .AddConverterFallback<DateTime, string>(x => x.ToString("o"))
            .AddConverterFallback<string, int>(x => int.TryParse(x, out var result) ? result : 0)
            .AddConverterFallback<string, double>(x => double.TryParse(x, out var result) ? result : 0)
            .Reset();
    }

    public static TDestination Map<TDestination>(
            this object source,
            Action<TDestination>? customMapping = null,
            MappingConfiguration? config = null,
            bool handleCircularReferences = true,
            bool caseInsensitive = false)
            where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();
        MapProperties(source, destination, config, handleCircularReferences ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null, caseInsensitive || _caseInsensitiveAutomaticMapping).GetAwaiter().GetResult();
        customMapping?.Invoke(destination);
        return destination;
    }

    public static async Task<TDestination> MapAsync<TDestination>(
             this object source,
             Func<TDestination, Task>? customMapping = null,
             MappingConfiguration? config = null,
             bool handleCircularReferences = true,
             bool caseInsensitive = false)
             where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();
        await MapProperties(source, destination, config, handleCircularReferences ? new HashSet<object>(ReferenceEqualityComparer.Instance) : null, caseInsensitive || _caseInsensitiveAutomaticMapping);
        if (customMapping != null) await customMapping(destination);
        return destination;
    }

    public static List<TDestination> MapCollection<TDestination>(
             this IEnumerable source,
             MappingConfiguration? config = null,
             bool caseInsensitive = false)
             where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destinationList = new List<TDestination>();
        foreach (var item in source)
        {
            if (item == null) continue;
            destinationList.Add(item.Map<TDestination>(config: config, caseInsensitive: caseInsensitive));
        }
        return destinationList;
    }

    public static async Task<List<TDestination>> MapCollectionAsync<TDestination>(
            this IEnumerable source,
            Func<TDestination, Task>? customMapping = null,
            MappingConfiguration? config = null,
            bool caseInsensitive = false)
            where TDestination : new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destinationList = new List<TDestination>();
        foreach (var item in source)
        {
            if (item == null) continue;
            destinationList.Add(await item.MapAsync(customMapping, config, caseInsensitive: caseInsensitive));
        }
        return destinationList;
    }

    private static async Task MapProperties(
            object source,
            object destination,
            MappingConfiguration? config,
            HashSet<object>? visited,
            bool caseInsensitive)
    {
        if (source == null || destination == null) return;
        if (visited?.Contains(source) == true) return;

        visited?.Add(source);

        var sourceType = source.GetType();
        var destType = destination.GetType();
        var sourceProps = MappingUtils.GetCachedProperties(sourceType);
        var destPropsDict = MappingUtils.GetCachedProperties(destType);
        var destProps = caseInsensitive
            ? new Dictionary<string, PropertyInfo>(destPropsDict.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase))
            : destPropsDict.ToDictionary(p => p.Name, p => p);
        var mappings = config?.PropertyMappings.GetValueOrDefault((sourceType, destType)) ?? new Dictionary<string, (string, int)>();

        var filteredMappings = mappings
            .GroupBy(m => m.Value.DestProp)
            .Select(g => g.OrderByDescending(m => m.Value.Priority).First())
            .ToDictionary(m => m.Key, m => m.Value);

        if (config == null || mappings.Count == 0)
        {
            foreach (var sourceProp in sourceProps)
            {
                if (destProps.TryGetValue(sourceProp.Name, out var destProp) && destProp.CanWrite &&
                    sourceProp.PropertyType.IsAssignableTo(destProp.PropertyType))
                {
                    var sourceValue = sourceProp.GetValue(source);
                    destProp.SetValue(destination, sourceValue);
                }
            }
        }
        else
        {
            foreach (var sourceProp in sourceProps)
            {
                var sourcePropName = sourceProp.Name;
                if (config.ExcludedProperties.Contains((sourceType, destType, sourcePropName)) ||
                    config._globallyExcludedProperties.Contains(sourcePropName))
                    continue;

                if (!filteredMappings.TryGetValue(sourcePropName, out var mapping))
                    continue;

                var (destPropName, _) = mapping;
                if (!destProps.TryGetValue(destPropName, out var destProp) || destProp == null || !destProp.CanWrite)
                {
                    if (config.DefaultValues.TryGetValue((sourceType, destType, destPropName), out var defaultValue))
                        destProp?.SetValue(destination, defaultValue);
                    continue;
                }

                if (config.ConditionalMappings.TryGetValue((sourceType, destType, sourcePropName), out var condition) &&
                    condition != null && !condition(source))
                    continue;

                var sourceValue = sourceProp.GetValue(source);
                if (sourceValue == null)
                {
                    var defaultKey = (sourceType, destType, destPropName);
                    var valueToSet = config.DefaultValues.ContainsKey(defaultKey) ? config.DefaultValues[defaultKey] : null;
                    destProp.SetValue(destination, valueToSet);
                    continue;
                }

                try
                {
                    object mappedValue = sourceValue;
                    if (config._composedPipelines.TryGetValue((sourceType, destType, sourcePropName), out var pipeline))
                    {
                        if (pipeline.AsyncConverter != null)
                            mappedValue = await (Task<object>)pipeline.AsyncConverter.DynamicInvoke(sourceValue)!;
                        else if (pipeline.Converter != null)
                            mappedValue = pipeline.Converter.DynamicInvoke(sourceValue)!;
                        else if (config.HasConverterFallback(sourceType, destType, sourceProp.PropertyType, destProp.PropertyType))
                        {
                            var fallback = config.GetConverterFallback(sourceType, destType, sourceProp.PropertyType, destProp.PropertyType);
                            if (fallback != null)
                                mappedValue = await fallback(sourceValue);
                        }

                        foreach (var (transformer, _) in pipeline.AsyncTransformers.OrderBy(t => t.Order))
                        {
                            mappedValue = await (Task<object>)transformer.DynamicInvoke(mappedValue)!;
                        }

                        foreach (var (transformer, _) in pipeline.Transformers.OrderBy(t => t.Order))
                        {
                            mappedValue = transformer.DynamicInvoke(mappedValue)!;
                        }
                    }
                    else
                    {
                        if (config._asyncTypeConverters.TryGetValue((sourceType, destType, sourcePropName), out var asyncConverter) && asyncConverter != null)
                        {
                            mappedValue = await (Task<object>)asyncConverter.DynamicInvoke(sourceValue)!;
                        }
                        else if (config._typeConverters.TryGetValue((sourceType, destType, sourcePropName), out var converter) && converter != null)
                        {
                            mappedValue = converter.DynamicInvoke(sourceValue)!;
                        }
                        else if (config.HasConverterFallback(sourceType, destType, sourceProp.PropertyType, destProp.PropertyType))
                        {
                            var fallback = config.GetConverterFallback(sourceType, destType, sourceProp.PropertyType, destProp.PropertyType);
                            if (fallback != null)
                                mappedValue = await fallback(sourceValue);
                        }

                        if (config._asyncPropertyTransformations.TryGetValue((sourceType, destType, sourcePropName), out var asyncTransformers) && asyncTransformers != null)
                        {
                            foreach (var (transformer, order) in asyncTransformers.OrderBy(t => t.Order))
                            {
                                mappedValue = await (Task<object>)transformer.DynamicInvoke(mappedValue)!;
                            }
                        }

                        if (config._propertyTransformations.TryGetValue((sourceType, destType, sourcePropName), out var transformers) && transformers != null)
                        {
                            foreach (var (transformer, order) in transformers.OrderBy(t => t.Order))
                            {
                                mappedValue = transformer.DynamicInvoke(mappedValue)!;
                            }
                        }
                    }

                    await MapProperty(sourceProp, destProp, mappedValue, destination, config, visited);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to map property '{sourcePropName}' from {sourceType.Name} to {destType.Name}: {ex.Message}",
                        ex);
                }
            }
        }

        visited?.Remove(source);
    }

    private static async Task MapProperty(
             PropertyInfo sourceProp,
             PropertyInfo destProp,
             object sourceValue,
             object destination,
             MappingConfiguration? config,
             HashSet<object>? visited)
    {
        if (MappingUtils.IsSimpleType(destProp.PropertyType))
        {
            if (!sourceProp.PropertyType.IsAssignableTo(destProp.PropertyType) &&
                (config == null || !config.HasConverterFallback(sourceProp.DeclaringType!, destProp.DeclaringType!, sourceProp.PropertyType, destProp.PropertyType)))
            {
                throw new InvalidOperationException(
                    $"Type mismatch: cannot map from {sourceProp.PropertyType.Name} to {destProp.PropertyType.Name}");
            }
            destProp.SetValue(destination, sourceValue);
        }
        else if (typeof(IEnumerable).IsAssignableFrom(destProp.PropertyType) && destProp.PropertyType != typeof(string))
        {
            var collection = await MapCollection(sourceValue as IEnumerable, destProp.PropertyType, config, visited);
            destProp.SetValue(destination, collection);
        }
        else
        {
            var nestedObject = Activator.CreateInstance(destProp.PropertyType);
            if (nestedObject == null)
                throw new InvalidOperationException($"Cannot create instance of {destProp.PropertyType.Name}");
            await MapProperties(sourceValue, nestedObject, config, visited, false); // Pass caseInsensitive
            destProp.SetValue(destination, nestedObject);
        }
    }

    private static async Task<object?> MapCollection(
            IEnumerable? source,
            Type destinationType,
            MappingConfiguration? config,
            HashSet<object>? visited)
    {
        if (source == null) return null;

        var itemType = destinationType.IsGenericType
            ? destinationType.GetGenericArguments()[0]
            : destinationType.GetElementType() ?? typeof(object);

        var listType = typeof(List<>).MakeGenericType(itemType);
        var destinationList = (IList?)Activator.CreateInstance(listType);
        if (destinationList == null)
            throw new InvalidOperationException($"Cannot create collection of type {listType.Name}");

        foreach (var item in source)
        {
            if (item == null || visited?.Contains(item) == true)
            {
                destinationList.Add(null);
                continue;
            }

            visited?.Add(item);
            object mappedItem;
            if (MappingUtils.IsSimpleType(item.GetType()))
            {
                mappedItem = item;
            }
            else
            {
                mappedItem = Activator.CreateInstance(itemType);
                if (mappedItem == null)
                    throw new InvalidOperationException($"Cannot create instance of {itemType.Name}");
                await MapProperties(item, mappedItem, config, visited, false); // Pass caseInsensitive
            }
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
}


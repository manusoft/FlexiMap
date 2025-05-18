using System.Linq.Expressions;
using System.Reflection;

namespace FlexiMap;

public class MappingConfiguration
{
    private readonly Dictionary<(Type Source, Type Destination), Dictionary<string, (string DestProp, int Priority)>> _propertyMappings = new();
    private readonly HashSet<(Type Source, Type Destination, string Property)> _excludedProperties = new();
    private readonly Dictionary<(Type Source, Type Destination, string Property), object> _defaultValues = new();
    private readonly Dictionary<(Type Source, Type Destination, string SourceProperty), Func<object, bool>> _conditionalMappings = new();    
    private readonly Dictionary<(Type Source, Type Dest), Dictionary<(Type SourceType, Type DestType), Func<object, Task<object>>>> _converterFallbacks = new();
    private readonly List<IConverterProvider> _converterProviders = new();
    
    public (Type Source, Type Destination)? _currentTypePair;
    public readonly Dictionary<(Type Source, Type Destination, string SourceProperty), Delegate> _typeConverters = new();
    public readonly Dictionary<(Type Source, Type Destination, string SourceProperty), Delegate> _asyncTypeConverters = new();
    public readonly Dictionary<(Type Source, Type Destination, string SourceProperty), List<(Delegate Transformer, int Order)>> _propertyTransformations = new();
    public readonly Dictionary<(Type Source, Type Destination, string SourceProperty), List<(Delegate Transformer, int Order)>> _asyncPropertyTransformations = new();
    public readonly Dictionary<(Type Source, Type Destination, string SourceProperty), (Delegate Converter, Delegate AsyncConverter, List<(Delegate Transformer, int Order)> Transformers, List<(Delegate AsyncTransformer, int Order)> AsyncTransformers)> _composedPipelines = new();
    public readonly HashSet<string> _globallyExcludedProperties = new();

    public Dictionary<(Type Source, Type Destination), Dictionary<string, (string DestProp, int Priority)>> PropertyMappings => _propertyMappings;
    public HashSet<(Type Source, Type Destination, string Property)> ExcludedProperties => _excludedProperties;
    public Dictionary<(Type Source, Type Destination, string Property), object> DefaultValues => _defaultValues;
    public Dictionary<(Type Source, Type Destination, string SourceProperty), Func<object, bool>> ConditionalMappings => _conditionalMappings;

    // Fluent API: Start configuration for a source and destination type pair
    public MappingConfiguration ForTypes<TSource, TDestination>()
    {
        _currentTypePair = (typeof(TSource), typeof(TDestination));
        return this;
    }

    // Fluent API: Map a source property to a destination property with optional priority
    public MappingConfiguration MapProperty(string sourceProperty, string destinationProperty, int priority = 0)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before mapping properties.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));

        var (sourceType, destType) = _currentTypePair.Value;
        if (!_propertyMappings.ContainsKey((sourceType, destType)))
            _propertyMappings[(sourceType, destType)] = new Dictionary<string, (string, int)>();

        _propertyMappings[(sourceType, destType)][sourceProperty] = (destinationProperty, priority);
        return this;
    }

    // Fluent API: Map properties using lambda expressions
    public MappingConfiguration MapProperty<TSource, TDestination, TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TDestination, TDestProp>> destinationProperty,
        int priority = 0)
    {
        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        return MapProperty(sourcePropName, destPropName, priority);
    }

    // Fluent API: Conditional property mapping
    public MappingConfiguration MapPropertyIf(
        string sourceProperty,
        string destinationProperty,
        Func<object, bool> condition,
        int priority = 0)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before mapping properties.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        var (sourceType, destType) = _currentTypePair.Value;
        MapProperty(sourceProperty, destinationProperty, priority);
        _conditionalMappings[(sourceType, destType, sourceProperty)] = condition;
        return this;
    }

    // Fluent API: Conditional property mapping with lambdas
    public MappingConfiguration MapPropertyIf<TSource, TDestination, TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TDestination, TDestProp>> destinationProperty,
        Func<TSource, bool> condition,
        int priority = 0)
    {
        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        return MapPropertyIf(sourcePropName, destPropName, src => condition((TSource)src), priority);
    }

    // Fluent API: Synchronous type converter
    public MappingConfiguration ConvertProperty<TSource, TDestination, TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TDestination, TDestProp>> destinationProperty,
        Func<TSourceProp, TDestProp> converter)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before configuring converters.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        var (sourceType, destType) = _currentTypePair.Value;

        MapProperty(sourcePropName, destPropName);
        _typeConverters[(sourceType, destType, sourcePropName)] = converter;
        return this;
    }

    // Fluent API: Asynchronous type converter
    public MappingConfiguration ConvertPropertyAsync<TSource, TDestination, TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TDestination, TDestProp>> destinationProperty,
        Func<TSourceProp, Task<TDestProp>> converter)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before configuring converters.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        var (sourceType, destType) = _currentTypePair.Value;

        MapProperty(sourcePropName, destPropName);
        _asyncTypeConverters[(sourceType, destType, sourcePropName)] = converter;
        return this;
    }

    // Fluent API: Synchronous property transformation with order
    public MappingConfiguration TransformProperty<TSource, TDestination, TProp>(
        Expression<Func<TSource, TProp>> sourceProperty,
        Expression<Func<TDestination, TProp>> destinationProperty,
        Func<TProp, TProp> transformer,
        int order = 0)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before configuring transformations.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (transformer == null)
            throw new ArgumentNullException(nameof(transformer));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        var (sourceType, destType) = _currentTypePair.Value;

        MapProperty(sourcePropName, destPropName);
        if (!_propertyTransformations.ContainsKey((sourceType, destType, sourcePropName)))
            _propertyTransformations[(sourceType, destType, sourcePropName)] = new List<(Delegate, int)>();
        _propertyTransformations[(sourceType, destType, sourcePropName)].Add((transformer, order));
        return this;
    }

    // Fluent API: Asynchronous property transformation with order
    public MappingConfiguration TransformPropertyAsync<TSource, TDestination, TProp>(
        Expression<Func<TSource, TProp>> sourceProperty,
        Expression<Func<TDestination, TProp>> destinationProperty,
        Func<TProp, Task<TProp>> transformer,
        int order = 0)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before configuring transformations.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));
        if (transformer == null)
            throw new ArgumentNullException(nameof(transformer));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        var (sourceType, destType) = _currentTypePair.Value;

        MapProperty(sourcePropName, destPropName);
        if (!_asyncPropertyTransformations.ContainsKey((sourceType, destType, sourcePropName)))
            _asyncPropertyTransformations[(sourceType, destType, sourcePropName)] = new List<(Delegate, int)>();
        _asyncPropertyTransformations[(sourceType, destType, sourcePropName)].Add((transformer, order));
        return this;
    }

    // Fluent API: Compose a pipeline of converter and transformations
    public MappingConfiguration ComposePipeline<TSource, TDestination, TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp>> sourceProperty,
        Expression<Func<TDestination, TDestProp>> destinationProperty,
        Func<TSourceProp, TDestProp>? converter = null,
        Func<TSourceProp, Task<TDestProp>>? asyncConverter = null,
        IEnumerable<(Func<TDestProp, TDestProp> Transformer, int Order)>? transformers = null,
        IEnumerable<(Func<TDestProp, Task<TDestProp>> Transformer, int Order)>? asyncTransformers = null)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before configuring pipelines.");

        if (sourceProperty == null)
            throw new ArgumentNullException(nameof(sourceProperty));
        if (destinationProperty == null)
            throw new ArgumentNullException(nameof(destinationProperty));

        var sourcePropName = GetPropertyName(sourceProperty);
        var destPropName = GetPropertyName(destinationProperty);
        var (sourceType, destType) = _currentTypePair.Value;

        MapProperty(sourcePropName, destPropName);
        _composedPipelines[(sourceType, destType, sourcePropName)] = (
            converter,
            asyncConverter,
            transformers?.Select(t => (Transformer: (Delegate)t.Transformer, t.Order)).ToList() ?? new List<(Delegate, int)>(),
            asyncTransformers?.Select(t => (Transformer: (Delegate)t.Transformer, t.Order)).ToList() ?? new List<(Delegate, int)>()
        );
        return this;
    }

    // Fluent API: Exclude a property from mapping
    public MappingConfiguration ExcludeProperty(string propertyName)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before excluding properties.");

        if (propertyName == null)
            throw new ArgumentNullException(nameof(propertyName));

        var (sourceType, destType) = _currentTypePair.Value;
        _excludedProperties.Add((sourceType, destType, propertyName));
        return this;
    }

    // Fluent API: Exclude a property using lambda
    public MappingConfiguration ExcludeProperty<TSource>(Expression<Func<TSource, object>> property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        var propName = GetPropertyName(property);
        return ExcludeProperty(propName);
    }

    // Fluent API: Set default value for a property
    public MappingConfiguration SetDefaultValue(string propertyName, object? defaultValue)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before setting default values.");

        if (propertyName == null)
            throw new ArgumentNullException(nameof(propertyName));

        var (sourceType, destType) = _currentTypePair.Value;
        _defaultValues[(sourceType, destType, propertyName)] = defaultValue; // Null-forgiving operator added
        return this;
    }

    // Fluent API: Set default value using lambda
    public MappingConfiguration SetDefaultValue<TDestination, TProp>(
        Expression<Func<TDestination, TProp>> property,
        TProp? defaultValue)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        var propName = GetPropertyName(property);
        return SetDefaultValue(propName, defaultValue);
    }

    // Fluent API: Globally exclude a property across all mappings
    public MappingConfiguration GloballyExcludeProperty(string propertyName)
    {
        if (propertyName == null)
            throw new ArgumentNullException(nameof(propertyName));

        _globallyExcludedProperties.Add(propertyName);
        return this;
    }

    // Fluent API: Register a converter fallback for a type pair
    public MappingConfiguration AddConverterFallback<TSourceType, TDestType>(Func<TSourceType, TDestType> converter)
    {
        if (_currentTypePair == null)
            throw new InvalidOperationException("Call ForTypes<TSource, TDestination>() before adding converter fallbacks.");

        if (converter == null)
            throw new ArgumentNullException(nameof(converter));

        var (sourceType, destType) = _currentTypePair.Value;
        if (!_converterFallbacks.ContainsKey((sourceType, destType)))
            _converterFallbacks[(sourceType, destType)] = new Dictionary<(Type, Type), Func<object, Task<object>>>();

        _converterFallbacks[(sourceType, destType)][(typeof(TSourceType), typeof(TDestType))] = x => Task.FromResult<object>(converter((TSourceType)x)!);
        return this;
    }

    // Fluent API: Register a converter provider
    public MappingConfiguration AddConverterProvider(IConverterProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        _converterProviders.Add(provider);
        return this;
    }

    // Fluent API: Validate the entire configuration
    public MappingConfiguration Validate(ValidationOptions? options = null)
    {
        return ValidateInternal(null, null, options);
    }

    // Fluent API: Batch validate specific type pairs or properties
    public MappingConfiguration Validate<TSource, TDestination>(
        Expression<Func<TSource, object>>[]? sourceProperties = null,
        ValidationOptions? options = null)
    {
        var typePair = (typeof(TSource), typeof(TDestination));
        var sourceProps = sourceProperties?.Select(GetPropertyName).ToHashSet();
        return ValidateInternal(new[] { typePair }, sourceProps, options);
    }

    private MappingConfiguration ValidateInternal(IEnumerable<(Type Source, Type Destination)>? typePairs = null, HashSet<string>? sourceProperties = null, ValidationOptions? options = null)
    {
        options ??= new ValidationOptions();
        var pairsToValidate = typePairs?.ToList() ?? _propertyMappings.Keys.ToList();

        foreach (var typePair in pairsToValidate)
        {
            var (sourceType, destType) = typePair;
            var sourceProps = MappingUtils.GetCachedProperties(sourceType).ToDictionary(p => p.Name);
            var destProps = MappingUtils.GetCachedProperties(destType).ToDictionary(p => p.Name);
            var mappings = _propertyMappings.GetValueOrDefault(typePair) ?? new Dictionary<string, (string, int)>();

            // Validate priority conflicts
            if (!options.SkipPriorityValidation)
            {
                var destPropMappings = mappings
                    .GroupBy(m => m.Value.DestProp)
                    .Where(g => g.Count() > 1)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Value.Priority).ToList());

                foreach (var destProp in destPropMappings.Keys)
                {
                    var orderedMappings = destPropMappings[destProp];
                    var highestPriority = orderedMappings[0].Value.Priority;
                    if (orderedMappings.Skip(1).Any(m => m.Value.Priority == highestPriority))
                        throw new InvalidOperationException(
                            $"Multiple mappings for destination property '{destProp}' in {sourceType.Name} to {destType.Name} have the same priority ({highestPriority}).");
                }
            }

            foreach (var (sourcePropName, (destPropName, _)) in mappings)
            {
                if (sourceProperties != null && !sourceProperties.Contains(sourcePropName))
                    continue;

                if (!options.SkipPropertyExistenceValidation)
                {
                    if (!sourceProps.ContainsKey(sourcePropName))
                        throw new InvalidOperationException($"Source property '{sourcePropName}' does not exist on type {sourceType.Name}.");
                    if (!destProps.ContainsKey(destPropName) || !destProps[destPropName].CanWrite)
                        throw new InvalidOperationException($"Destination property '{destPropName}' does not exist or is not writable on type {destType.Name}.");
                }

                var sourceProp = sourceProps.GetValueOrDefault(sourcePropName);
                var destProp = destProps.GetValueOrDefault(destPropName);
                if (sourceProp == null || destProp == null) continue;

                if (!_typeConverters.ContainsKey((sourceType, destType, sourcePropName)) &&
                    !_asyncTypeConverters.ContainsKey((sourceType, destType, sourcePropName)) &&
                    !HasConverterFallback(sourceType, destType, sourceProp.PropertyType, destProp.PropertyType) &&
                    !sourceProp.PropertyType.IsAssignableTo(destProp.PropertyType))
                {
                    throw new InvalidOperationException(
                        $"Type mismatch for property '{sourcePropName}' mapping from {sourceType.Name} to {destType.Name}: " +
                        $"{sourceProp.PropertyType.Name} is not assignable to {destProp.PropertyType.Name}. Consider adding a type converter.");
                }

                // Validate transformations
                if (!options.SkipTransformationTypeValidation)
                {
                    if (_propertyTransformations.TryGetValue((sourceType, destType, sourcePropName), out var transformers))
                    {
                        foreach (var (transformer, _) in transformers)
                        {
                            if (transformer.Method.ReturnType != sourceProp.PropertyType ||
                                transformer.Method.GetParameters()[0].ParameterType != sourceProp.PropertyType)
                                throw new InvalidOperationException(
                                    $"Transformation for property '{sourcePropName}' in {sourceType.Name} to {destType.Name} has incompatible type.");
                        }
                    }

                    if (_asyncPropertyTransformations.TryGetValue((sourceType, destType, sourcePropName), out var asyncTransformers))
                    {
                        foreach (var (transformer, _) in asyncTransformers)
                        {
                            if (transformer.Method.ReturnType != typeof(Task<>).MakeGenericType(sourceProp.PropertyType) ||
                                transformer.Method.GetParameters()[0].ParameterType != sourceProp.PropertyType)
                                throw new InvalidOperationException(
                                    $"Async transformation for property '{sourcePropName}' in {sourceType.Name} to {destType.Name} has incompatible type.");
                        }
                    }
                }
            }

            if (!options.SkipPropertyExistenceValidation)
            {
                foreach (var (sType, dType, prop) in _excludedProperties)
                {
                    if (sType == sourceType && dType == destType && (!sourceProps.ContainsKey(prop) || (sourceProperties != null && !sourceProperties.Contains(prop))))
                        throw new InvalidOperationException($"Excluded property '{prop}' does not exist on type {sourceType.Name}.");
                }

                foreach (var (sType, dType, prop) in _defaultValues.Keys)
                {
                    if (sType == sourceType && dType == destType && (!destProps.ContainsKey(prop) || (sourceProperties != null && !sourceProperties.Contains(prop))))
                        throw new InvalidOperationException($"Default value property '{prop}' does not exist on type {destType.Name}.");
                }
            }
        }

        return this;
    }

    // Fluent API: Reset the current type pair
    public MappingConfiguration Reset()
    {
        _currentTypePair = null;
        return this;
    }

    // Helper to check if a converter fallback exists
    internal bool HasConverterFallback(Type sourceType, Type destType, Type sourcePropType, Type destPropType)
    {
        if (_converterFallbacks.GetValueOrDefault((sourceType, destType))?.ContainsKey((sourcePropType, destPropType)) == true)
            return true;

        var converterMethods = typeof(MappingConfiguration).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<ConverterFallbackAttribute>() != null);
        foreach (var method in converterMethods)
        {
            var attr = method.GetCustomAttribute<ConverterFallbackAttribute>();
            if (attr == null) continue;
            if (attr.SourceType == sourcePropType && attr.DestinationType == destPropType)
                return true;
        }

        foreach (var provider in _converterProviders)
        {
            if (provider.GetConverters().Any(c => c.SourceType == sourcePropType && c.DestType == destPropType))
                return true;
        }

        return false;
    }

    // Helper to get a converter fallback
    public Func<object, Task<object>>? GetConverterFallback(Type sourceType, Type destType, Type sourcePropType, Type destPropType)
    {
        if (_converterFallbacks.GetValueOrDefault((sourceType, destType))?.TryGetValue((sourcePropType, destPropType), out var fallbackConverter) == true)
            return fallbackConverter;

        var converterMethods = typeof(MappingConfiguration).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.GetCustomAttribute<ConverterFallbackAttribute>() != null);
        foreach (var method in converterMethods)
        {
            var attr = method.GetCustomAttribute<ConverterFallbackAttribute>();
            if (attr == null) continue;
            if (attr.SourceType == sourcePropType && attr.DestinationType == destPropType)
            {
                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                    return async x => await (Task<object>)method.Invoke(null, new[] { x })!;
                return x => Task.FromResult(method.Invoke(null, new[] { x })!);
            }
        }

        foreach (var provider in _converterProviders)
        {
            var converter = provider.GetConverters().FirstOrDefault(c => c.SourceType == sourcePropType && c.DestType == destPropType);
            if (converter.Converter != null)
                return converter.Converter;
        }

        return null;
    }

    // Helper to extract property name from lambda expression
    private static string GetPropertyName<T, TProp>(Expression<Func<T, TProp>> expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));

        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
            return unaryMember.Member.Name;
        throw new ArgumentException("Expression must be a property access.", nameof(expression));
    }

    // Dynamic fallback converters
    [ConverterFallbackAttribute(typeof(int), typeof(string))]
    private static string IntToString(int value) => value.ToString();

    [ConverterFallbackAttribute(typeof(double), typeof(string))]
    private static string DoubleToString(double value) => value.ToString();

    [ConverterFallbackAttribute(typeof(DateTime), typeof(string))]
    private static string DateTimeToString(DateTime value) => value.ToString("o");

    [ConverterFallbackAttribute(typeof(string), typeof(int))]
    private static int StringToInt(string value) => int.TryParse(value, out var result) ? result : 0;

    [ConverterFallbackAttribute(typeof(string), typeof(double))]
    private static double StringToDouble(string value) => double.TryParse(value, out var result) ? result : 0;

    // Non-fluent methods for backward compatibility
    public void AddPropertyMapping(Type sourceType, Type destType, string sourceProp, string destProp)
    {
        if (sourceType == null)
            throw new ArgumentNullException(nameof(sourceType));
        if (destType == null)
            throw new ArgumentNullException(nameof(destType));
        if (sourceProp == null)
            throw new ArgumentNullException(nameof(sourceProp));
        if (destProp == null)
            throw new ArgumentNullException(nameof(destProp));

        var key = (sourceType, destType);
        if (!_propertyMappings.ContainsKey(key))
            _propertyMappings[key] = new Dictionary<string, (string, int)>();
        _propertyMappings[key][sourceProp] = (destProp, 0);
    }

    public void ExcludeProperty(Type sourceType, Type destType, string propertyName)
    {
        if (sourceType == null)
            throw new ArgumentNullException(nameof(sourceType));
        if (destType == null)
            throw new ArgumentNullException(nameof(destType));
        if (propertyName == null)
            throw new ArgumentNullException(nameof(propertyName));

        _excludedProperties.Add((sourceType, destType, propertyName));
    }
}


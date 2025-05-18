namespace FlexiMap;
using System.Collections.Generic;

// Helper class to handle reference equality for circular reference tracking
public class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();
    private ReferenceEqualityComparer() { }
    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
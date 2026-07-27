using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Blex.Generators;

/// <summary>
/// Immutable array wrapper with structural equality so generator pipeline models cache correctly.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[] _items;

    public EquatableArray(T[] items) => _items = items;

    public int Count => _items?.Length ?? 0;

    public T this[int index] => _items[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (Count != other.Count)
            return false;
        for (var i = 0; i < Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_items[i], other._items[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_items is null)
            return 0;
        unchecked
        {
            var hash = 17;
            foreach (var item in _items)
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

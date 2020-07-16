using System;
using System.Collections.Generic;

class Cache<TOutput, TKey>
{
    Dictionary<TKey, TOutput> _cache { get; } = new Dictionary<TKey, TOutput>();

    public TOutput GetOrMake(TKey input, Func<TOutput> factory)
    {
        if (_cache.TryGetValue(input, out var result))
            return result;
        result = factory();
        _cache.Add(input, result);
        return result;
    }
}
using AtlasDrop.Core.Performance;

namespace AtlasDrop.Infrastructure.Performance;

public sealed class MemoryTtlCache<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private sealed record CacheItem(
        TValue Value,
        DateTime ExpiresUtc);

    private readonly object _gate = new();
    private readonly Dictionary<TKey, CacheItem> _items = new();

    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var item))
            {
                value = default;
                return false;
            }

            if (DateTime.UtcNow >= item.ExpiresUtc)
            {
                _items.Remove(key);
                value = default;
                return false;
            }

            value = item.Value;
            return true;
        }
    }

    public void Set(TKey key, TValue value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl));

        lock (_gate)
        {
            _items[key] = new CacheItem(
                value,
                DateTime.UtcNow.Add(ttl));
        }
    }

    public void Remove(TKey key)
    {
        lock (_gate)
        {
            _items.Remove(key);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
        }
    }
}

namespace AtlasDrop.Core.Performance;

public interface ICache<TKey, TValue>
    where TKey : notnull
{
    bool TryGet(TKey key, out TValue? value);

    void Set(TKey key, TValue value, TimeSpan ttl);

    void Remove(TKey key);

    void Clear();
}

using Microsoft.Extensions.Caching.Memory;

namespace SemanticSearch.Services;

public class CachingEmbedder : IEmbedder, IDisposable
{
    private readonly IEmbedder _inner;
    private readonly IMemoryCache _cache;
    private readonly MemoryCache _ownCache;
    private readonly int _maxEntries;

    public CachingEmbedder(IEmbedder inner, int maxEntries = 2048)
    {
        _inner = inner;
        _ownCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = maxEntries });
        _cache = _ownCache;
        _maxEntries = maxEntries;
    }

    public void Fit(IEnumerable<string> corpus)
    {
        _inner.Fit(corpus);
    }

    public float[] Embed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        var key = NormalizeKey(text);
        if (_cache.TryGetValue<float[]>(key, out var vec)) return vec;
        vec = _inner.Embed(text);
        // store with size=1 entry
        _cache.Set(key, vec, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(30) });
        return vec;
    }

    private static string NormalizeKey(string s) => s.Trim().ToLowerInvariant();

    public void Dispose()
    {
        _ownCache?.Dispose();
        if (_inner is IDisposable d) d.Dispose();
    }
}

using System.Collections.Concurrent;

namespace SemanticSearch.Services;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, float[]> _vecs = new();

    public void Upsert(string id, float[] vector, object? meta = null)
    {
        _vecs[id] = vector;
    }

    public IEnumerable<(string id, float cosine)> Similar(float[] query, int topK)
    {
        static float Cos(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a.Count == 0 || b.Count == 0) return 0f;
            int len = Math.Min(a.Count, b.Count);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++)
            {
                var x = a[i];
                var y = b[i];
                dot += x * y;
                na += x * x;
                nb += y * y;
            }
            if (na == 0 || nb == 0) return 0f;
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
        }

        return _vecs.Select(kv => (kv.Key, Cos(query, kv.Value)))
                    .OrderByDescending(t => t.Item2)
                    .Take(topK);
    }
}

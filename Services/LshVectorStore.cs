using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SemanticSearch.Services;

public class LshVectorStore : IVectorStore
{
    private int _numTables;
    private int _numPlanes;
    private int _maxCandidates;

    private readonly ConcurrentDictionary<string, float[]> _vecs = new();

    // Per table: signature (ulong) -> set of ids
    private ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>[] _tables;

    // Per table: planes[planeIndex][dimension]
    private float[][][]? _planes;
    private int _dim = -1;
    private int _seed;
    private readonly object _lock = new();

    public LshVectorStore(int numTables = 8, int numPlanes = 24, int maxCandidates = 2000, int seed = 42)
    {
        if (numPlanes <= 0 || numPlanes > 64)
            throw new ArgumentOutOfRangeException(nameof(numPlanes), "numPlanes must be in (0, 64].");

        _numTables = numTables;
        _numPlanes = numPlanes;
        _maxCandidates = Math.Max(200, maxCandidates);
        _seed = seed;

        _tables = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>[_numTables];
        for (int i = 0; i < _numTables; i++)
            _tables[i] = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>();
    }

    public LshVectorStoreOptions GetOptions() => new()
    {
        NumTables = _numTables,
        NumPlanes = _numPlanes,
        MaxCandidates = _maxCandidates,
        Seed = _seed
    };

    public void Reconfigure(LshVectorStoreOptions opts)
    {
        if (opts is null) throw new ArgumentNullException(nameof(opts));
        if (opts.NumPlanes <= 0 || opts.NumPlanes > 64) throw new ArgumentOutOfRangeException(nameof(opts.NumPlanes));
        if (opts.NumTables <= 0) throw new ArgumentOutOfRangeException(nameof(opts.NumTables));
        if (opts.MaxCandidates < 200) throw new ArgumentOutOfRangeException(nameof(opts.MaxCandidates));

        lock (_lock)
        {
            _numTables = opts.NumTables;
            _numPlanes = opts.NumPlanes;
            _maxCandidates = opts.MaxCandidates;
            _seed = opts.Seed;

            // reset planes so they regenerate with new config
            _planes = null;

            // recreate tables
            _tables = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>[_numTables];
            for (int i = 0; i < _numTables; i++)
                _tables[i] = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>>();

            // Rehash if we know dimension
            int dim = _dim;
            if (dim < 0 && !_vecs.IsEmpty)
            {
                // infer from first vector
                dim = _vecs.First().Value.Length;
            }

            if (dim > 0 && !_vecs.IsEmpty)
            {
                EnsureInitialized(dim);
                foreach (var kv in _vecs)
                {
                    var v = kv.Value; // already normalized in Upsert
                    for (int t = 0; t < _numTables; t++)
                    {
                        var sig = Hash(v, _planes![t]);
                        var bucket = _tables[t].GetOrAdd(sig, static _ => new ConcurrentDictionary<string, byte>());
                        bucket.TryAdd(kv.Key, 1);
                    }
                }
            }
        }
    }

    public void Upsert(string id, float[] vector, object? meta = null)
    {
        if (vector is null || vector.Length == 0) return;

        EnsureInitialized(vector.Length);

        var v = Normalize(vector);
        _vecs[id] = v;

        for (int t = 0; t < _numTables; t++)
        {
            var sig = Hash(v, _planes![t]);
            var bucket = _tables[t].GetOrAdd(sig, static _ => new ConcurrentDictionary<string, byte>());
            bucket.TryAdd(id, 1);
        }
    }

    public IEnumerable<(string id, float cosine)> Similar(float[] query, int topK)
    {
        if (query is null || query.Length == 0 || _vecs.IsEmpty)
            return Enumerable.Empty<(string, float)>();

        EnsureInitialized(query.Length);

        // Normalize query (cosine => dot of unit vectors)
        var q = Normalize(query);

        // Collect candidate ids from all tables
        var candidates = new HashSet<string>();
        for (int t = 0; t < _numTables; t++)
        {
            var sig = Hash(q, _planes![t]);
            if (_tables[t].TryGetValue(sig, out var bucket))
            {
                foreach (var kv in bucket)
                {
                    if (candidates.Count >= _maxCandidates) break;
                    candidates.Add(kv.Key);
                }
            }
        }

        // Fallback if buckets are too sparse
        if (candidates.Count == 0)
            candidates.UnionWith(_vecs.Keys.Take(_maxCandidates));

        // Rank by cosine (dot because normalized)
        static float Dot(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            int len = Math.Min(a.Count, b.Count);
            double s = 0;
            for (int i = 0; i < len; i++) s += a[i] * b[i];
            return (float)s;
        }

        var scored = candidates
            .Select(id =>
            {
                var ok = _vecs.TryGetValue(id, out var dv);
                var score = ok ? Dot(q, dv!) : 0f;
                return (id, score);
            })
            .OrderByDescending(t => t.score)
            .Take(topK);

        return scored;
    }

    private void EnsureInitialized(int dim)
    {
        if (_planes != null) return;

        // First initialization is racy but deterministic in outcome
        lock (_lock)
        {
            if (_planes != null) return;

            _dim = dim;
            _planes = new float[_numTables][][];
            var rng = new Random(_seed);

            for (int t = 0; t < _numTables; t++)
            {
                _planes[t] = new float[_numPlanes][];
                for (int p = 0; p < _numPlanes; p++)
                {
                    var plane = new float[_dim];
                    for (int d = 0; d < _dim; d++)
                        plane[d] = (float)NextGaussian(rng); // N(0,1)
                    // Normalize plane to unit length
                    var n = Norm(plane);
                    if (n > 0)
                    {
                        for (int d = 0; d < _dim; d++)
                            plane[d] /= n;
                    }
                    _planes[t][p] = plane;
                }
            }
        }
    }

    private static double NextGaussian(Random rng)
    {
        // Box-Muller
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static float Norm(IReadOnlyList<float> v)
    {
        double s = 0;
        for (int i = 0; i < v.Count; i++) s += v[i] * v[i];
        return (float)Math.Sqrt(s);
    }

    private static float[] Normalize(IReadOnlyList<float> v)
    {
        var n = Norm(v);
        if (n == 0) return v.ToArray();
        var r = new float[v.Count];
        for (int i = 0; i < v.Count; i++) r[i] = v[i] / n;
        return r;
    }

    private static ulong Hash(IReadOnlyList<float> v, IReadOnlyList<float[]> planes)
    {
        ulong sig = 0UL;
        for (int p = 0; p < planes.Count; p++)
        {
            double dot = 0;
            var plane = planes[p];
            int len = Math.Min(v.Count, plane.Length);
            for (int i = 0; i < len; i++) dot += v[i] * plane[i];
            if (dot >= 0) sig |= (1UL << p);
        }
        return sig;
    }
}

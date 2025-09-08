using System.Collections.Concurrent;
using SemanticSearch.Models;

namespace SemanticSearch.Services;

public class SimpleLexicalStore : ILexicalStore
{
    private readonly ConcurrentDictionary<string, Document> _docs;

    public SimpleLexicalStore(IEnumerable<Document> docs)
    {
        _docs = new ConcurrentDictionary<string, Document>(
            docs.Select(d => new KeyValuePair<string, Document>(d.Id.ToString(), d)));
    }

    // naive BM25-ish: term frequency in Title+Content; use contains count
    public IEnumerable<(string id, double score)> Search(string query, int topK)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<(string, double)>();
        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        double Score(Document d)
        {
            var text = (d.Title + "\n" + d.Content).ToLowerInvariant();
            double s = 0;
            foreach (var t in terms)
            {
                int idx = 0, c = 0;
                while ((idx = text.IndexOf(t, idx, StringComparison.Ordinal)) >= 0) { c++; idx += t.Length; }
                if (c > 0) s += 1 + Math.Log(1 + c); // very rough tf boost
            }
            return s;
        }

        return _docs.Select(kv => (kv.Key, Score(kv.Value)))
                    .Where(t => t.Item2 > 0)
                    .OrderByDescending(t => t.Item2)
                    .Take(topK);
    }
}

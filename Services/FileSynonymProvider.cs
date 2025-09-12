using System.Text.RegularExpressions;
using System.Text;

namespace SemanticSearch.Services;

public class FileSynonymProvider : ISynonymProvider
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<string>> _map = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Map => _map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);

    public string? SourcePath { get; }

    public FileSynonymProvider(string? path)
    {
        SourcePath = path;
        Reload();
    }

    public void Reload()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !File.Exists(SourcePath)) return;
        var lines = File.ReadAllLines(SourcePath, Encoding.UTF8);
        lock (_lock)
        {
            _map.Clear();
            foreach (var line in lines)
            {
                var clean = line.Trim();
                if (string.IsNullOrEmpty(clean) || clean.StartsWith('#')) continue;
                var parts = clean.Split("=>", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2) continue;
                var root = parts[0].Trim();
                var syns = parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (!_map.TryGetValue(root, out var list))
                {
                    list = new List<string>();
                    _map[root] = list;
                }
                foreach (var s in syns)
                {
                    if (!list.Contains(s, StringComparer.OrdinalIgnoreCase)) list.Add(s);
                }
            }
        }
    }

    public string Expand(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return query;
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expanded = new List<string>();
        lock (_lock)
        {
            foreach (var t in tokens)
            {
                expanded.Add(t);
                if (_map.TryGetValue(t, out var syns))
                {
                    expanded.AddRange(syns);
                }
            }
        }
        return string.Join(' ', expanded.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

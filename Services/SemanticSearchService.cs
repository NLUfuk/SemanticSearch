using Bogus;
using Microsoft.ML;
using SemanticSearch.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SemanticSearch.Services;

public interface ISemanticSearchService
{
    IReadOnlyList<Document> AllDocuments { get; }
    (IReadOnlyList<SearchResult> results, int total) Search(string? query, int take = 20, float alpha = 0.4f, bool hybrid = true, string type = "hybrid");
}

public class SemanticSearchService : ISemanticSearchService, IDisposable
{
    private readonly MLContext _ml;
    private readonly List<Document> _docs;

    private readonly IEmbedder _embedder;
    private readonly float[][] _docVectors;

    private readonly IVectorStore _vecStore;
    private readonly ILexicalStore _lexStore;

    public IReadOnlyList<Document> AllDocuments => _docs;

    public SemanticSearchService(IVectorStore vecStore, IEmbedder embedder, ISynonymProvider? synonyms = null)
    {
        _ml = new MLContext(seed: 42);
        _embedder = embedder;

        _docs = GenerateFakeDocuments(1000);

        var corpus = _docs.Select(d => BuildSearchText(d)).ToList();
        _embedder.Fit(corpus);

        _docVectors = corpus.Select(c => _embedder.Embed(c)).ToArray();

        _vecStore = vecStore;
        for (int i = 0; i < _docs.Count; i++)
        {
            _vecStore.Upsert(_docs[i].Id.ToString(), _docVectors[i], _docs[i]);
        }

        _lexStore = new LuceneLexicalStore(_docs, synonyms);
    }

    public (IReadOnlyList<SearchResult> results, int total) Search(string? query, int take = 20, float alpha = 0.4f, bool hybrid = true, string type = "hybrid")
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (Array.Empty<SearchResult>(), _docs.Count);
        }

        type = type?.ToLowerInvariant();
        return type switch
        {
            "lexical" => RunLexical(query, take),
            "semantic" => RunSemantic(query, take),
            _ => RunHybrid(query, take, alpha)
        };
    }

    private (IReadOnlyList<SearchResult>, int) RunLexical(string query, int take)
    {
        var hits = _lexStore.Search(query, take).ToList();
        var results = hits.Select(x => new SearchResult
        {
            Document = _docs.First(d => d.Id.ToString() == x.id),
            Score = (float)x.score
        }).ToList();
        return (results, _docs.Count);
    }

    private (IReadOnlyList<SearchResult>, int) RunSemantic(string query, int take)
    {
        var qv = _embedder.Embed(query);
        var hits = _vecStore.Similar(qv, take).ToList();
        var results = hits.Select(x => new SearchResult
        {
            Document = _docs.First(d => d.Id.ToString() == x.id),
            Score = x.cosine
        }).ToList();
        return (results, _docs.Count);
    }

    private (IReadOnlyList<SearchResult>, int) RunHybrid(string query, int take, float alpha)
    {
        var k = Math.Max(take * 5, 50);
        var qVec = _embedder.Embed(query);
        var vecTop = _vecStore.Similar(qVec, k).ToDictionary(t => t.id, t => (double)t.cosine);
        var lexTop = _lexStore.Search(query, k).ToDictionary(t => t.id, t => t.score);

        static Dictionary<string, double> Normalize(Dictionary<string, double> src)
        {
            if (src.Count == 0) return src;
            var max = src.Values.Max();
            if (max <= 0) return src.ToDictionary(kv => kv.Key, kv => 0d);
            return src.ToDictionary(kv => kv.Key, kv => kv.Value / max);
        }

        var vN = Normalize(vecTop);
        var lN = Normalize(lexTop);
        var ids = new HashSet<string>(vN.Keys.Concat(lN.Keys));

        var merged = ids.Select(id => new
        {
            id,
            score = alpha * (lN.TryGetValue(id, out var ls) ? ls : 0) + (1 - alpha) * (vN.TryGetValue(id, out var vs) ? vs : 0)
        })
        .OrderByDescending(x => x.score)
        .Take(take)
        .Select(x => new SearchResult
        {
            Document = _docs.First(d => d.Id.ToString() == x.id),
            Score = (float)x.score
        })
        .ToList();

        return (merged, _docs.Count);
    }

    private static string BuildSearchText(Document d)
    {
        var titleBoost = string.Join(' ', Enumerable.Repeat(d.Title, 2));
        var categoryBoost = string.Join(' ', Enumerable.Repeat(d.Category, 2));
        return string.Join('\n', new[]
        {
            titleBoost,
            d.Content,
            categoryBoost,
            d.SubmitterName,
            d.SubmitterGender,
            d.SubmitterCity,
            $"yas {d.SubmitterAge}",
            d.SubmitterPhone
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static List<Document> GenerateFakeDocuments(int count)
    {
        var faker = new Bogus.Faker("tr");
        var genders = new[] { "Erkek", "Kadýn" };
        var cities = new[] { "Ýstanbul", "Ankara", "Ýzmir", "Bursa", "Antalya", "Adana", "Konya", "Gaziantep", "Denizli" };

        var list = new List<Document>(capacity: count);
        for (int i = 0; i < count; i++)
        {
            var gen = TextGenerator.Generate();
            var doc = new Document
            {
                Id = i,
                Title = gen.Title,
                Content = gen.Content,
                Category = gen.MainTopic,
                SubmitterName = faker.Name.FullName(),
                SubmitterAge = faker.Random.Int(18, 75),
                SubmitterPhone = faker.Phone.PhoneNumber("05#########"),
                SubmitterGender = faker.PickRandom(genders),
                SubmitterCity = faker.PickRandom(cities)
            };
            list.Add(doc);
        }
        return list;
    }

    public void Dispose()
    {
        if (_lexStore is IDisposable disp)
            disp.Dispose();
    }
}

using Bogus;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using SemanticSearch.Models;

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

    // ML.NET objects for text featurization (vector baseline)
    private readonly ITransformer _featurizer;
    private readonly float[][] _docVectors; // cached vectors for docs

    // Hybrid components
    private readonly IVectorStore _vecStore;
    private readonly ILexicalStore _lexStore;

    public IReadOnlyList<Document> AllDocuments => _docs;

    public SemanticSearchService(IVectorStore vecStore)
    {
        _ml = new MLContext(seed: 42);

        // 1) Generate fake documents
        _docs = GenerateFakeDocuments(1000);

        // 2) Vector featurizer (hashed n-grams)
        var data = _ml.Data.LoadFromEnumerable(_docs.Select(d => new DocInput { Text = BuildSearchText(d) }));

        var featurizeOptions = new TextFeaturizingEstimator.Options
        {
            CaseMode = TextNormalizingEstimator.CaseMode.Lower,
            KeepDiacritics = true, // müzik != muzik
            KeepNumbers = true,
            KeepPunctuations = false,
            // Kelime n-gramlarý kalsýn, karakter n-gramlarýný kapat
            WordFeatureExtractor = new WordBagEstimator.Options
            {
                NgramLength = 2,
                UseAllLengths = true
            },
            CharFeatureExtractor = null
        };
        var pipeline = _ml.Transforms.Text.FeaturizeText(
            outputColumnName: "Features",
            inputColumnName: nameof(DocInput.Text)
            // Remove: options: featurizeOptions
);
        _featurizer = pipeline.Fit(data);

        // 3) Cache vectors and fill vector store
        var transformed = _featurizer.Transform(data);
        _docVectors = _ml.Data.CreateEnumerable<DocFeatures>(transformed, reuseRowObject: false)
            .Select(r => r.Features)
            .Select(v => v is null ? Array.Empty<float>() : v)
            .ToArray();

        // Use injected vector store and populate
        _vecStore = vecStore;
        for (int i = 0; i < _docs.Count; i++)
        {
            _vecStore.Upsert(_docs[i].Id.ToString(), _docVectors[i], _docs[i]);
        }

        // 4) Lucene lexical store (TurkishAnalyzer + BM25 + fuzzy)
        _lexStore = new LuceneLexicalStore(_docs);
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
        var qv = Vectorize(query);
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
        var qVec = Vectorize(query);
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

    private float[] Vectorize(string text)
    {
        var input = new[] { new DocInput { Text = text } };
        var view = _ml.Data.LoadFromEnumerable(input);
        var transformed = _featurizer.Transform(view);
        var row = _ml.Data.CreateEnumerable<DocFeatures>(transformed, reuseRowObject: false).First();
        return row.Features ?? Array.Empty<float>();
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

    private class DocInput
    {
        public string Text { get; set; } = string.Empty;
    }

    private class DocFeatures
    {
        [VectorType]
        public float[]? Features { get; set; }
    }
}

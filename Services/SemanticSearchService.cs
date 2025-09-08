using Bogus;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using SemanticSearch.Models;

namespace SemanticSearch.Services;

public interface ISemanticSearchService
{
    IReadOnlyList<Document> AllDocuments { get; }
    (IReadOnlyList<SearchResult> results, int total) Search(string? query, int take = 20);
}

public class SemanticSearchService : ISemanticSearchService
{
    private readonly MLContext _ml;
    private readonly List<Document> _docs;

    // ML.NET objects for text featurization
    private readonly ITransformer _featurizer;
    private readonly DataViewSchema _schema;

    private readonly float[][] _docVectors; // cached vectors for docs

    public IReadOnlyList<Document> AllDocuments => _docs;

    public SemanticSearchService()
    {
        _ml = new MLContext(seed: 42);

        // 1) Generate fake documents
        _docs = GenerateFakeDocuments(1000);

        // 2) Build featurization pipeline (use FeaturizeText with hashing to handle unseen words)
        var data = _ml.Data.LoadFromEnumerable(_docs.Select(d => new DocInput { Text = BuildSearchText(d) }));
        var pipeline = _ml.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(DocInput.Text));

        _featurizer = pipeline.Fit(data);
        _schema = _featurizer.GetOutputSchema(data.Schema);

        // 3) Cache vectors for all docs for fast search
        var transformed = _featurizer.Transform(data);
        _docVectors = _ml.Data.CreateEnumerable<DocFeatures>(transformed, reuseRowObject: false)
            .Select(r => r.Features)
            .Select(v => v is null ? Array.Empty<float>() : v)
            .ToArray();
    }

    public (IReadOnlyList<SearchResult> results, int total) Search(string? query, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (Array.Empty<SearchResult>(), _docs.Count);
        }

        var queryVec = Vectorize(query);

        var scored = _docs.Select((d, i) => new SearchResult
        {
            Document = d,
            Score = CosineSimilarity(queryVec, _docVectors[i])
        })
        .OrderByDescending(r => r.Score)
        .Take(take)
        .ToList();

        return (scored, _docs.Count);
    }

    private float[] Vectorize(string text)
    {
        var input = new[] { new DocInput { Text = text } };
        var view = _ml.Data.LoadFromEnumerable(input);
        var transformed = _featurizer.Transform(view);
        var row = _ml.Data.CreateEnumerable<DocFeatures>(transformed, reuseRowObject: false).First();
        return row.Features ?? Array.Empty<float>();
    }

    private static float CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
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

    private static string BuildSearchText(Document d)
    {
        // Ýçerikte gönderen bilgilerini de indeksle ki aramada görünsün
        return string.Join('\n', new[]
        {
            d.Title,
            d.Content,
            d.Category,
            d.SubmitterName,
            d.SubmitterGender,
            d.SubmitterCity,
            $"yas {d.SubmitterAge}",
            d.SubmitterPhone
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static List<Document> GenerateFakeDocuments(int count)
    {
        var faker = new Faker("tr");
        var categories = new[] { "Teknoloji", "Spor", "Ekonomi", "Kültür", "Saðlýk" };

        var genders = new[] { "Erkek", "Kadýn" };
        var cities = new[] { "Ýstanbul", "Ankara", "Ýzmir", "Bursa", "Antalya", "Adana", "Konya", "Gaziantep","Denizli" };

        var docFaker = new Faker<Document>("tr")
            .RuleFor(d => d.Id, f => f.IndexFaker)
            .RuleFor(d => d.Title, f => f.Lorem.Sentence(4, 6))
            .RuleFor(d => d.Content, f => f.Lorem.Paragraphs(2, 4))
            .RuleFor(d => d.Category, f => f.PickRandom(categories))
            // submitter
            .RuleFor(d => d.SubmitterName, f => f.Name.FullName())
            .RuleFor(d => d.SubmitterAge, f => f.Random.Int(18, 75))
            .RuleFor(d => d.SubmitterPhone, f => f.Phone.PhoneNumber("05#########"))
            .RuleFor(d => d.SubmitterGender, f => f.PickRandom(genders))
            .RuleFor(d => d.SubmitterCity, f => f.PickRandom(cities));

        return docFaker.Generate(count);
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

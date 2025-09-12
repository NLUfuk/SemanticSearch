using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using System.Collections.Concurrent;

namespace SemanticSearch.Services;

/// <summary>
/// ML.NET tabanlý basit n-gram featurizer embedder. (Yerel bag-of-words vektörü)
/// </summary>
public class MlNetEmbedder : IEmbedder
{
    private readonly MLContext _ml;
    private ITransformer? _model;
    private DataViewSchema? _schema;
    private readonly object _lock = new();

    private class InputRow { public string Text { get; set; } = string.Empty; }

    private class OutputRow { [VectorType] public float[]? Features { get; set; } }

    public MlNetEmbedder(MLContext? ctx = null)
    {
        _ml = ctx ?? new MLContext(seed: 42);
    }

    public void Fit(IEnumerable<string> corpus)
    {
        // idempotent: eðer zaten eðitildiyse tekrar eðitme
        if (_model != null) return;
        lock (_lock)
        {
            if (_model != null) return;
            var data = _ml.Data.LoadFromEnumerable(corpus.Select(t => new InputRow { Text = t }));
            var pipeline = _ml.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                inputColumnName: nameof(InputRow.Text));
            _model = pipeline.Fit(data);
            _schema = data.Schema;
        }
    }

    public float[] Embed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        if (_model is null)
            throw new InvalidOperationException("Embedder not fitted. Call Fit() first.");

        var view = _ml.Data.LoadFromEnumerable(new[] { new InputRow { Text = text } });
        var transformed = _model.Transform(view);
        var row = _ml.Data.CreateEnumerable<OutputRow>(transformed, reuseRowObject: false).First();
        return row.Features ?? Array.Empty<float>();
    }
}

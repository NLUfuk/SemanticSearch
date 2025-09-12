using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SemanticSearch.Services;

public class OnnxEmbedder : IEmbedder, IDisposable
{
    private readonly EmbeddingOptions _opts;
    private InferenceSession? _session;
    private readonly object _lock = new();

    private readonly Dictionary<string, int> _vocab = new(StringComparer.Ordinal);
    private const string UnkToken = "[UNK]";
    private const string ClsToken = "[CLS]";
    private const string SepToken = "[SEP]";

    public OnnxEmbedder(EmbeddingOptions opts)
    {
        _opts = opts;
        PrepareFilesIfNeeded();
    }

    private void PrepareFilesIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(_opts.OnnxModelPath))
            throw new InvalidOperationException("OnnxModelPath ayarlanmalý.");
        if (!File.Exists(_opts.OnnxModelPath))
        {
            if (_opts.AutoDownload)
            {
                TryDownloadModel();
            }
            if (!File.Exists(_opts.OnnxModelPath))
                throw new FileNotFoundException("Onnx model bulunamadý", _opts.OnnxModelPath);
        }
        if (!string.IsNullOrWhiteSpace(_opts.OnnxVocabPath))
        {
            if (!File.Exists(_opts.OnnxVocabPath))
            {
                if (_opts.AutoDownload)
                {
                    TryDownloadVocab();
                }
            }
            if (!File.Exists(_opts.OnnxVocabPath))
                throw new FileNotFoundException("Vocab dosyasý bulunamadý", _opts.OnnxVocabPath);
            LoadVocab(_opts.OnnxVocabPath);
        }
    }

    private void TryDownloadModel()
    {
        if (string.IsNullOrWhiteSpace(_opts.Model)) return;
        var url = $"{_opts.HuggingFaceBaseUrl.TrimEnd('/')}/{_opts.Model}/resolve/main/model.onnx";
        Directory.CreateDirectory(Path.GetDirectoryName(_opts.OnnxModelPath!)!);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_opts.DownloadTimeoutSec) };
        var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        File.WriteAllBytes(_opts.OnnxModelPath!, bytes);
    }

    private void TryDownloadVocab()
    {
        if (string.IsNullOrWhiteSpace(_opts.Model) || string.IsNullOrWhiteSpace(_opts.OnnxVocabPath)) return;
        var url = $"{_opts.HuggingFaceBaseUrl.TrimEnd('/')}/{_opts.Model}/resolve/main/vocab.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(_opts.OnnxVocabPath!)!);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(_opts.DownloadTimeoutSec) };
        var txt = http.GetStringAsync(url).GetAwaiter().GetResult();
        File.WriteAllText(_opts.OnnxVocabPath!, txt, Encoding.UTF8);
    }

    public void Fit(IEnumerable<string> corpus) => EnsureSession();

    private void EnsureSession()
    {
        if (_session is not null) return;
        lock (_lock)
        {
            if (_session is null)
            {
                _session = new InferenceSession(_opts.OnnxModelPath!);
            }
        }
    }

    public float[] Embed(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        EnsureSession();
        if (_session is null) return Array.Empty<float>();

        var tokens = BasicTokenize(text).Take(_opts.MaxSeqLength - 2).ToList();
        tokens.Insert(0, ClsToken);
        tokens.Add(SepToken);

        var ids = tokens.Select(t => _vocab.TryGetValue(t, out var id) ? id : (_vocab.TryGetValue(UnkToken, out var u) ? u : 0)).ToArray();
        var mask = Enumerable.Repeat(1, ids.Length).ToArray();
        var typeIds = new int[ids.Length];

        int maxLen = _opts.MaxSeqLength;
        if (ids.Length < maxLen)
        {
            ids = ids.Concat(Enumerable.Repeat(0, maxLen - ids.Length)).ToArray();
            mask = mask.Concat(Enumerable.Repeat(0, maxLen - mask.Length)).ToArray();
            typeIds = typeIds.Concat(Enumerable.Repeat(0, maxLen - typeIds.Length)).ToArray();
        }
        else if (ids.Length > maxLen)
        {
            ids = ids.Take(maxLen).ToArray();
            mask = mask.Take(maxLen).ToArray();
            typeIds = typeIds.Take(maxLen).ToArray();
        }

        var inputIds = new DenseTensor<long>(new[] {1, maxLen});
        var attention = new DenseTensor<long>(new[] {1, maxLen});
        var tokenType = new DenseTensor<long>(new[] {1, maxLen});
        for (int i = 0; i < maxLen; i++)
        {
            inputIds[0, i] = ids[i];
            attention[0, i] = mask[i];
            tokenType[0, i] = typeIds[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention)
        };
        if (_session.InputMetadata.ContainsKey("token_type_ids"))
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenType));

        using var results = _session.Run(inputs);
        var first = results.First();
        var tensor = first.AsTensor<float>();
        // shape [1, seq, hidden]
        if (tensor.Dimensions.Length == 3)
        {
            int seq = tensor.Dimensions[1];
            int hidden = tensor.Dimensions[2];

            if (_opts.Pooling.Equals("mean", StringComparison.OrdinalIgnoreCase))
            {
                var mean = new float[hidden];
                for (int t = 0; t < seq; t++)
                {
                    for (int h = 0; h < hidden; h++)
                        mean[h] += tensor[0, t, h];
                }
                for (int h = 0; h < hidden; h++) mean[h] /= seq;
                if (_opts.L2Normalize) L2Norm(mean);
                return mean;
            }
            else // CLS
            {
                var cls = new float[hidden];
                for (int h = 0; h < hidden; h++) cls[h] = tensor[0, 0, h];
                if (_opts.L2Normalize) L2Norm(cls);
                return cls;
            }
        }
        var flat = tensor.ToArray();
        if (_opts.L2Normalize) L2Norm(flat);
        return flat;
    }

    private static void L2Norm(float[] v)
    {
        double s = 0; for (int i = 0; i < v.Length; i++) s += v[i] * v[i];
        if (s <= 0) return; var inv = 1.0 / Math.Sqrt(s);
        for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] * inv);
    }

    private void LoadVocab(string path)
    {
        int i = 0;
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var token = line.Trim();
            if (token.Length == 0) continue;
            if (!_vocab.ContainsKey(token)) _vocab[token] = i++;
        }
        if (!_vocab.ContainsKey(UnkToken)) _vocab[UnkToken] = i++;
        if (!_vocab.ContainsKey(ClsToken)) _vocab[ClsToken] = i++;
        if (!_vocab.ContainsKey(SepToken)) _vocab[SepToken] = i++;
    }

    private static IEnumerable<string> BasicTokenize(string text)
    {
        text = text.ToLowerInvariant();
        foreach (var part in Regex.Split(text, "[^a-z0-9çðýöþü]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            yield return part;
        }
    }

    public void Dispose() => _session?.Dispose();
}

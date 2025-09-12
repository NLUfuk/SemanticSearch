namespace SemanticSearch.Services;

public class EmbeddingOptions
{
    public string Provider { get; set; } = "mlnet"; // mlnet | onnx | huggingface
    public string Model { get; set; } = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"; // huggingface model id
    public string? ApiKey { get; set; } // huggingface api anahtarý
    public string? Endpoint { get; set; } // openai/özel endpoint
    public string? OnnxModelPath { get; set; } // onnx dosya yolu
    public string? OnnxVocabPath { get; set; } // vocab.txt yolu
    public int MaxSeqLength { get; set; } = 128;
    public string Pooling { get; set; } = "cls"; // cls | mean
    public bool L2Normalize { get; set; } = true;
    public bool AutoDownload { get; set; } = false; // model/vocab yoksa HF'den indir
    public int DownloadTimeoutSec { get; set; } = 60;
    public string HuggingFaceBaseUrl { get; set; } = "https://huggingface.co"; // raw resolve kullanýlacak
}

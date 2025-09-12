namespace SemanticSearch.Services;

public interface IEmbedder
{
    // Fit / initialize embedding model on a corpus (idempotent). Can be no-op for external API embedders.
    void Fit(IEnumerable<string> corpus);
    // Embed a single text into a float vector.
    float[] Embed(string text);
}

public interface IVectorStore
{
    void Upsert(string id, float[] vector, object? meta = null);
    IEnumerable<(string id, float cosine)> Similar(float[] query, int topK);
}

public interface ILexicalStore
{
    IEnumerable<(string id, double score)> Search(string query, int topK);
}

public interface IRanker
{
    IEnumerable<(string id, double score)> Rerank(string query, IEnumerable<(string id, double score)> candidates, int topK);
}

public interface ISynonymProvider
{
    string Expand(string query);
    IReadOnlyDictionary<string, IReadOnlyList<string>> Map { get; }
}

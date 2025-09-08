using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tr;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using SemanticSearch.Models;
using LuceneDirectory = Lucene.Net.Store.Directory;
using LuceneDocument = Lucene.Net.Documents.Document;
using LuceneStringField = Lucene.Net.Documents.StringField;
using LuceneTextField = Lucene.Net.Documents.TextField;
using LuceneField = Lucene.Net.Documents.Field;
using LuceneRAMDirectory = Lucene.Net.Store.RAMDirectory;

namespace SemanticSearch.Services;

public class LuceneLexicalStore : ILexicalStore, IDisposable
{
    private readonly Analyzer _analyzer;
    private readonly LuceneDirectory _dir;
    private readonly IndexWriter _writer;
    private readonly IndexSearcher _searcher;
    private readonly IndexReader _reader;

    private const LuceneVersion LV = LuceneVersion.LUCENE_48;

    public LuceneLexicalStore(IEnumerable<SemanticSearch.Models.Document> docs)
    {
        _analyzer = new TurkishAnalyzer(LV);
        _dir = new LuceneRAMDirectory();
        _writer = new IndexWriter(_dir, new IndexWriterConfig(LV, _analyzer));

        foreach (var d in docs)
        {
            var doc = new LuceneDocument
            {
                new LuceneStringField("id", d.Id.ToString(), LuceneField.Store.YES),
                new LuceneTextField("title", d.Title ?? string.Empty, LuceneField.Store.NO),
                new LuceneTextField("content", d.Content ?? string.Empty, LuceneField.Store.NO),
                new LuceneTextField("category", d.Category ?? string.Empty, LuceneField.Store.NO),
                new LuceneTextField("submitter", string.Join(" ", new[]{ d.SubmitterName, d.SubmitterGender, d.SubmitterCity, d.SubmitterPhone, $"yas {d.SubmitterAge}" }), LuceneField.Store.NO)
            };
            _writer.AddDocument(doc);
        }
        _writer.Commit();

        _reader = DirectoryReader.Open(_writer, applyAllDeletes: true);
        _searcher = new IndexSearcher(_reader); // BM25Similarity by default
    }

    public IEnumerable<(string id, double score)> Search(string query, int topK)
    {
        if (string.IsNullOrWhiteSpace(query)) yield break;
        // Multi-field query: boost title and category
        var qp = new MultiFieldQueryParser(LV,
            new[] { "title", "content", "category", "submitter" },
            _analyzer)
        {
            DefaultOperator = QueryParserBase.AND_OPERATOR // set AND as default
        };

        var parsed = qp.Parse(QueryParser.Escape(query));
        // Boost fields manually by wrapping same parsed query with boosts
        var titleQ = qp.Parse(QueryParser.Escape(query));
        titleQ.Boost = 2.0f;
        var categoryQ = qp.Parse(QueryParser.Escape(query));
        categoryQ.Boost = 1.5f;
        var boolean = new BooleanQuery
        {
            { parsed, Occur.SHOULD },
            { titleQ, Occur.SHOULD },
            { categoryQ, Occur.SHOULD }
        };

        // Fuzzy: add small fuzzy variant per term (edit distance 1)
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var t in terms)
        {
            var fq = new FuzzyQuery(new Term("content", t.ToLowerInvariant()), maxEdits: 1);
            boolean.Add(fq, Occur.SHOULD);
        }

        var hits = _searcher.Search(boolean, topK).ScoreDocs;
        foreach (var h in hits)
        {
            var doc = _searcher.Doc(h.Doc);
            yield return (doc.Get("id"), h.Score);
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _dir?.Dispose();
        _analyzer?.Dispose();
    }
}

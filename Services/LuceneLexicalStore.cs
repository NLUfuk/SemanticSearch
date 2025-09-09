using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tr;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using SemanticSearch.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
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

        var escaped = QueryParser.Escape(query);
        var parsed = qp.Parse(escaped);
        var titleQ = qp.Parse(escaped); titleQ.Boost = 2.0f;
        var categoryQ = qp.Parse(escaped); categoryQ.Boost = 1.5f;
        var boolean = new BooleanQuery
        {
            { parsed, Occur.SHOULD },
            { titleQ, Occur.SHOULD },
            { categoryQ, Occur.SHOULD }
        };

        // Restrict fuzzy to avoid "müzik" -> "fizik" style matches
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var alpha = new Regex("^[A-Za-zÇÐÝÖÞÜçðýöþü]+$", RegexOptions.CultureInvariant);
        foreach (var t in terms.Select(s => s.ToLowerInvariant()))
        {
            if (t.Length >= 5 && alpha.IsMatch(t))
            {
                var fq = new FuzzyQuery(new Term("content", t), maxEdits: 1, prefixLength: 2, maxExpansions: 50, transpositions: true);
                boolean.Add(fq, Occur.SHOULD);
            }
        }

        if (boolean.Clauses.Count > 0)
        {
            boolean.MinimumNumberShouldMatch = 1;
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

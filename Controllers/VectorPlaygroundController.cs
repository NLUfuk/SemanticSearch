using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Models;
using SemanticSearch.Services;

namespace SemanticSearch.Controllers;

[ApiController]
[Route("api/vector")] 
public class VectorPlaygroundController : ControllerBase
{
    private readonly IVectorStore _vec;
    private readonly IEmbedder _embedder;
    private readonly ISemanticSearchService _svc;

    public VectorPlaygroundController(IVectorStore vec, IEmbedder embedder, ISemanticSearchService svc)
    {
        _vec = vec;
        _embedder = embedder;
        _svc = svc;
    }

    public record VectorQuery(float[] Vector, int TopK = 10);
    public record TextQuery(string Text, int TopK = 10);
    public record UpsertRequest(string Id, float[] Vector);

    [HttpGet("info")]
    public IActionResult Info()
    {
        var dim = _embedder.Embed("test").Length;
        return Ok(new { Dimension = dim });
    }

    [HttpPost("search")]
    public IActionResult Search([FromBody] VectorQuery query)
    {
        if (query?.Vector == null || query.Vector.Length == 0)
            return BadRequest("vector required");
        var hits = _vec.Similar(query.Vector, Math.Max(1, query.TopK)).ToList();
        var docs = _svc.AllDocuments;
        var results = hits.Select(h => new {
            id = h.id,
            score = h.cosine,
            title = docs.FirstOrDefault(d => d.Id.ToString() == h.id)?.Title,
            category = docs.FirstOrDefault(d => d.Id.ToString() == h.id)?.Category,
            snippet = docs.FirstOrDefault(d => d.Id.ToString() == h.id)?.Content?.Substring(0, Math.Min(200, (docs.FirstOrDefault(d => d.Id.ToString() == h.id)?.Content ?? string.Empty).Length))
        });
        return Ok(results);
    }

    [HttpPost("embed-search")]
    public IActionResult EmbedAndSearch([FromBody] TextQuery query)
    {
        if (query == null || string.IsNullOrWhiteSpace(query.Text))
            return BadRequest("text required");
        var v = _embedder.Embed(query.Text);
        return Search(new VectorQuery(v, query.TopK));
    }

    [HttpPost("upsert")]
    public IActionResult Upsert([FromBody] UpsertRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Id) || req.Vector == null || req.Vector.Length == 0)
            return BadRequest("id and vector required");
        _vec.Upsert(req.Id, req.Vector);
        return NoContent();
    }
}

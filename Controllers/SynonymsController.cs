using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Services;
using System.Text;

namespace SemanticSearch.Controllers;

[Route("api/synonyms")]
[ApiController]
public class SynonymsController : ControllerBase
{
    private readonly ISynonymProvider _provider;

    public SynonymsController(ISynonymProvider provider)
    {
        _provider = provider;
    }

    [HttpGet]
    public IActionResult Get()
    {
        if (_provider is FileSynonymProvider fsp && !string.IsNullOrWhiteSpace(fsp.SourcePath) && System.IO.File.Exists(fsp.SourcePath))
        {
            var text = System.IO.File.ReadAllText(fsp.SourcePath, Encoding.UTF8);
            return Content(text, "text/plain; charset=utf-8");
        }
        // fallback: serialize map
        var lines = _provider.Map.Select(kv => $"{kv.Key} => {string.Join(", ", kv.Value)}");
        return Content(string.Join("\n", lines), "text/plain; charset=utf-8");
    }

    [HttpPost]
    public IActionResult Save([FromBody] string content)
    {
        if (_provider is not FileSynonymProvider fsp || string.IsNullOrWhiteSpace(fsp.SourcePath))
            return BadRequest("File-based provider deðil.");

        System.IO.File.WriteAllText(fsp.SourcePath!, content ?? string.Empty, Encoding.UTF8);
        fsp.Reload();
        return NoContent();
    }
}

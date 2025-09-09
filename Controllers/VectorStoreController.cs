using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SemanticSearch.Services;

namespace SemanticSearch.Controllers;

[ApiController]
[Route("api/vector-store")] 
public class VectorStoreController : ControllerBase
{
    private readonly IVectorStore _store;

    public VectorStoreController(IVectorStore store)
    {
        _store = store;
    }

    [HttpGet("options")]
    public ActionResult<LshVectorStoreOptions> GetOptions()
    {
        if (_store is LshVectorStore lsh)
        {
            return Ok(lsh.GetOptions());
        }
        return BadRequest("Vector store does not support runtime options.");
    }

    [HttpPost("options")]
    public IActionResult UpdateOptions([FromBody] LshVectorStoreOptions options)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (_store is LshVectorStore lsh)
        {
            lsh.Reconfigure(options);
            return NoContent();
        }
        return BadRequest("Vector store does not support runtime options.");
    }
}

using Microsoft.AspNetCore.Mvc;
using MedicalRag.Api.Services;

namespace MedicalRag.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController : ControllerBase
{
    private readonly VectorStoreService _vectorStore;

    public RagController(VectorStoreService vectorStore)
    {
        _vectorStore = vectorStore;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request)
    {
        // 1. Generate query embedding via Ollama
        // 2. Fetch top-K similar medical chunks from pgvector
        // 3. Pass context + question to Llama 3.2 via Semantic Kernel
        // 4. Return response with source citations
        
        return Ok(new { Answer = "Sample grounded medical answer.", Sources = new[] { "Paper_1.pdf" } });
    }
}

public record QuestionRequest(string Question);
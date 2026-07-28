// Api gateway that recieves HTTP requests, validates them and returns HTTP response
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using MedicalRag.Api.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedicalRag.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// provides access to API response methods without loading UI 
public class RagController : ControllerBase
{
    // automatically provides a pre-configured instance of VectorStoreServices
    private readonly VectorStoreService _vectorStore;
    private readonly DocumentIngestionService _ingestionService;
    private readonly IChatCompletionService _chatService;
    private readonly HttpClient _httpClient;

    public RagController(
        VectorStoreService vectorStore,
        DocumentIngestionService ingestionService,
        IchatCompletionService chatService,
        HttpClient httpClient
    )
    {
        // stores it and uses it throughtout the controller
        _vectorStore = vectorStore;
        _ingestionService = ingestionService;
        _chatService = chatService;
        _httpClient = httpClient;
    }
    // appends ask to base route in the final post endpoint 
    [HttpPost("ask")]
    // extracts json body from http request and maps to request variable
    public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request)
    {
        // Generate query embedding via Ollama
        // Fetch top-K similar medical chunks from pgvector
        // Pass context + question to Llama 3.2 via Semantic Kernel
        // Return response with source citations
        
        return Ok(new { Answer = "Sample grounded medical answer.", Sources = new[] { "Paper_1.pdf" } });
    }
}

// Defining datatype of expected from this endpoint
public record QuestionRequest(string Question);
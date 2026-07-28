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
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file, [FromForm] string title, [FromForm] string? pmid)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.\nPlease upload a valid .pdf file.");

        using var stream = file.OpenReadStream();
        var chunkCount = await _ingestionService.ProcessPdfAsync(stream, title, pmid);

        return Ok(new { Message = "Document processed successfully!", ChunksSaved = chunkCount }); 
    }

    // appends ask to base route in the final post endpoint 
    [HttpPost("ask")]
    // extracts json body from http request and maps to request variable
    public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request)
    {
        // Generate query embedding via Ollama
        var queryEmbedding = await GenerateQueryEmbeddingAsync(request.Question);
        
        // Fetch top-K similar medical chunks from pgvector
        var topContext = await _vectorStore.SearchSimilarChunksAsync(queryEmbedding, 3);
        var combinedContext = string.Join("\n\n", topContext); 
        
        // assemble grounded prompt
        var systemPrompt = $"""
                    You are a medical research assistant. Answer the user's prompt strictly using the provided paper excerpts. 
                    If the information is not explicitly present in the retrieved chunks, state: 'Information not found in the provided medical literature.'
                    
                    CONTEXT:
                    {combinedContext}
                    """;
        var chatHistory = new ChatHistory(systemPrompt);
        chatHistory.AddUserMessage(request.Question);

        // Pass context + question to Llama 3.2 via Semantic Kernel
        var response = await _chatService.GetChatMessageContentAsync(chatHistory);
        
        // Return response with source citations
        return Ok(new { Answer = response.Content, Sources = topContext });
    }
}

// Defining datatype of expected from this endpoint
public record QuestionRequest(string Question);
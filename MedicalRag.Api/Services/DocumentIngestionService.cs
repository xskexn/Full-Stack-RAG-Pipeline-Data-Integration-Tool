// Parsing and embedding engine takes raw medical pdf, extracts readable text, breaks it into chucnks and converts them into vectors then stores it in the database
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace MedicalRag.Api.Services;

public class DocumentIngestionService
{
    private readonly VectorStoreService _vectorStore;
    private readonly HttpClient _httpClient;

    public DocumentIngestionService(VectorStoreService vectorStore, HttpClient httpClient)
    {
    // request instance of VectorStoreService to save data to PostgreSQL
        _vectorStore = vectorStore;
    // sets up HttpClient to make web request with Ollama 
        _httpClient = httpClient;
    }

    public async Task<int> ProcessPdfAsync(Stream pdfStream, string documentTitle, string? pmidDoi = null)
    {
        // Extracts text from PDF using PdfPig to a StringBuilder
        var fullTextBuilder = new StringBuilder();
        // Error handling for empty text and bad pdfs to prevent crash
        using (var pdf = PdfDocument.Open(pdfStream))
        {
            foreach (var page in pdf.GetPages())
            {
                fullTextBuilder.AppendLine(page.Text);
            }
        }

        var fullText = fullTextBuilder.ToString();
        if (string.IsNullOrWhiteSpace(fullText))
        {
            throw new InvalidOperationException("Failed to extract readable text from PDF.");
        }

        // Chunk text with overlapping windowes concept: chucnks of 500 char with 100 char overlap
        var chunks = ChunkText(fullText, chunkSize: 500, overlap: 100);

        // Generate math vector for each chunk via Ollama and saves it to the DB
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embedding = await GenerateEmbeddingFromOllamaAsync(chunkText);
            await _vectorStore.SaveChunkAsync(documentTitle, pmidDoi, i, chunkText, embedding);
        }
        // returns total number of processed chunks
        return chunks.Count; 
    }

    // Sclices the document into bite-sized pieces: Sliding window token algorithm
    private List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        int startIndex = 0;
        // Scans through the enire document lenght
        while (startIndex < text.Length)
        {
            // preventing outOfBound EOF errors from crashign server
            int length = Math.Min(chunkSize, text.Length - startIndex);
            var chunk = text.Substring(startIndex, length).Trim();
            
            // preventing redundant whiteLines from entering the db 
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            // Sliding window concept moves forward by chunk size - overlap
            startIndex += chunkSize - overlap;
            if (startIndex >= text.Length - overlap) break;
        }

        return chunks;
    }

    // translates string of text into a vector array using ollama instance
    private async Task<float[]> GenerateEmbeddingFromOllamaAsync(string text)
    {
        var requestPayload = new
        {
            model = "nomic-embed-text",
            prompt = text
        };

        // sends asynchronous POST request to Ollama API endpoints
        var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/embeddings", requestPayload);
        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var embeddingElement = jsonDoc.RootElement.GetProperty("embedding");

        // parses JSON response retruned by Ollama
        var embeddings = new float[embeddingElement.GetArrayLength()];
        int idx = 0;
        foreach (var val in embeddingElement.EnumerateArray())
        {
            embeddings[idx++] = val.GetSingle();
        }

        return embeddings;
    }
}
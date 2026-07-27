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
        _vectorStore = vectorStore;
        _httpClient = httpClient;
    }

    public async Task<int> ProcessPdfAsync(Stream pdfStream, string documentTitle, string? pmidDoi = null)
    {
        // Extracts text from PDF using PdfPig
        var fullTextBuilder = new StringBuilder();
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

        // Chunk text with overlapping window concept
        var chunks = ChunkText(fullText, chunkSize: 500, overlap: 100);

        // Generate embedding for each chunk via Ollama and saves it to the DB
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkText = chunks[i];
            var embedding = await GenerateEmbeddingFromOllamaAsync(chunkText);
            await _vectorStore.SaveChunkAsync(documentTitle, pmidDoi, i, chunkText, embedding);
        }

        return chunks.Count;
    }

    private List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        int startIndex = 0;

        while (startIndex < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - startIndex);
            var chunk = text.Substring(startIndex, length).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            startIndex += chunkSize - overlap;
            if (startIndex >= text.Length - overlap) break;
        }

        return chunks;
    }

    private async Task<float[]> GenerateEmbeddingFromOllamaAsync(string text)
    {
        var requestPayload = new
        {
            model = "nomic-embed-text",
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/embeddings", requestPayload);
        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var embeddingElement = jsonDoc.RootElement.GetProperty("embedding");

        var embeddings = new float[embeddingElement.GetArrayLength()];
        int idx = 0;
        foreach (var val in embeddingElement.EnumerateArray())
        {
            embeddings[idx++] = val.GetSingle();
        }

        return embeddings;
    }
}
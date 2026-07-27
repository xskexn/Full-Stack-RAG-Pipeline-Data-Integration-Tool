// Database layer for PostgreSQL keeping SQL commands isolated from core logic

using Npgsql;
using Pgvector;

namespace MedicalRag.Api.Services;

// Injects IConfiguration interface to access application 
public class VectorStoreService
{
    private readonly string _connectionString;

    public VectorStoreService(IConfiguration config)
    {
        // lookup and retrival for DefaultConnection
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }
    // extract specific chunks of medical text, metadata and mathetical vector storing it storing into the db
    public async Task SaveChunkAsync(string title, string pmid, int index, string text, float[] embedding)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var query = @"
            INSERT INTO medical_document_chunks (document_title, pmid_doi, chunk_index, chunk_text, embedding)
            VALUES (@title, @pmid, @index, @text, @embedding)";

        await using var cmd = new NpgsqlCommand(query, conn);
        // maps c# variable to SQL parameters
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("pmid", pmid ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("index", index);
        cmd.Parameters.AddWithValue("text", text);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));

        await cmd.ExecuteNonQueryAsync();
    }

// retrival step takes in vector representation of user question and finds most semantically relevant medical chunk stored in the db
    public async Task<List<string>> SearchSimilarChunksAsync(float[] queryEmbedding, int topK = 3)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Cosine distance similarity search operator: <=>
        var query = @"
            SELECT chunk_text, document_title
            FROM medical_document_chunks
            ORDER BY embedding <=> @queryEmbedding
            LIMIT @topK"; // LIMIT @topk returns the most relevant 3 results

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("queryEmbedding", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("topK", topK);

        // returns results in a list
        var results = new List<string>();

        // reads the returned rows
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            //formats the output prepends doc title and source doc, adds it to the return list
            var text = reader.GetString(0);
            var title = reader.GetString(1);
            results.Add($"[Source: {title}]\n{text}");
        }

        return results;
    }
}
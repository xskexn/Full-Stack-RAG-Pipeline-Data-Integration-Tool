using Npgsql;
using Pgvector;

namespace MedicalRag.Api.Services;

public class VectorStoreService
{
    private readonly NpgsqlDataSource _dataSource;

    public VectorStoreService(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")!;
        
        // Build a data source and explicitly register the pgvector extension
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector(); 
        
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task SaveChunkAsync(string title, string? pmid, int index, string text, float[] embedding)
    {
        // Open the connection using the pre-configured data source
        await using var conn = await _dataSource.OpenConnectionAsync();

        var query = @"
            INSERT INTO medical_document_chunks (document_title, pmid_doi, chunk_index, chunk_text, embedding)
            VALUES (@title, @pmid, @index, @text, @embedding)";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("pmid", pmid ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("index", index);
        cmd.Parameters.AddWithValue("text", text);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<string>> SearchSimilarChunksAsync(float[] queryEmbedding, int topK = 3)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Cosine distance similarity search operator: <=>
        var query = @"
            SELECT chunk_text, document_title
            FROM medical_document_chunks
            ORDER BY embedding <=> @queryEmbedding
            LIMIT @topK";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("queryEmbedding", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("topK", topK);

        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var text = reader.GetString(0);
            var title = reader.GetString(1);
            results.Add($"[Source: {title}]\n{text}");
        }

        return results;
    }
}
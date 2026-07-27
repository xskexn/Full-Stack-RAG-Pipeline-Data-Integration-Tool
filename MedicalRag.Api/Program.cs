using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddHttpClient<DocumentIngestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

// Automatically initialises pgvector extension and table on startup
InitializeDatabase(app.Configuration.GetConnectionString("DefaultConnection")!);

app.Run();

static void InitializeDatabase(string connectionString)
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();

    // 1. Enable pgvector extension
    using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
    {
        cmd.ExecuteNonQuery();
    }

    // 2. Create medical document chunks table
    var createTableSql = @"
        CREATE TABLE IF NOT EXISTS medical_document_chunks (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            document_title TEXT NOT NULL,
            pmid_doi TEXT,
            chunk_index INT NOT NULL,
            chunk_text TEXT NOT NULL,
            embedding vector(768)
        );

        CREATE INDEX IF NOT EXISTS idx_medical_chunks_embedding 
        ON medical_document_chunks 
        USING hnsw (embedding vector_cosine_ops);
    ";

    using (var cmd = new NpgsqlCommand(createTableSql, conn))
    {
        cmd.ExecuteNonQuery();
    }
}
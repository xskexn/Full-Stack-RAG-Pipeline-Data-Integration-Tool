// Assembles database connection, routing and api enpoints into web server
using Npgsql;
using Microsoft.SemanticKernel;
using MedicalRag.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// add services to the container
builder.Services.AddOpenApi();
builder.Services.AddControllers();
// creates shared instance of service database  
builder.Services.AddSingleton<VectorStoreService>();
builder.Services.AddHttpClient<DocumentIngestionService>();
// adds generic HttpClient for the controller
#pragma warning disable SKEXP0070
builder.Services.AddHttpClient(); 
builder.Services.AddKernel().AddOllamaChatCompletion(
        modelId: "llama3.2",
        endpoint: new Uri("http://localhost:11434"));
#pragma warning restore SKEXP0070

// locks in registred services above and creates the application 
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
    // opens direct connection to postgresSQL using NpgsqlConnection
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();

    // enables pgvector extension
    using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
    {
        cmd.ExecuteNonQuery();
    }

    // creates medical document chunks table 
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
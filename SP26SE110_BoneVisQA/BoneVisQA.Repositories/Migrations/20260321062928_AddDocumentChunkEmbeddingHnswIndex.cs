using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Adds an HNSW index on document_chunks.embedding for cosine similarity search.
    /// This migration is intentionally minimal: the database schema already exists (e.g. Supabase);
    /// we only apply the index required by the RAG pipeline.
    /// </summary>
    public partial class AddDocumentChunkEmbeddingHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_document_chunks_embedding"
                ON document_chunks
                USING hnsw (embedding vector_cosine_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_document_chunks_embedding";
                """);
        }
    }
}

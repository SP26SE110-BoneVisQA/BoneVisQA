using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

/// <summary>Adds pgvector embedding and indexing pipeline fields for <c>medical_cases</c> RAG.</summary>
public partial class AddMedicalCaseEmbeddingIndexing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE medical_cases ADD COLUMN IF NOT EXISTS embedding vector(768);
            ALTER TABLE medical_cases ADD COLUMN IF NOT EXISTS indexing_status text NOT NULL DEFAULT 'Pending';
            ALTER TABLE medical_cases ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 1;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'medical_cases' AND column_name = 'reflective_questions'
              ) THEN
                ALTER TABLE medical_cases ADD COLUMN reflective_questions text;
              END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            UPDATE medical_cases SET indexing_status = 'Pending' WHERE embedding IS NULL;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_medical_cases_embedding"
            ON medical_cases
            USING hnsw (embedding vector_cosine_ops);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_medical_cases_indexing_status_pending"
            ON medical_cases (indexing_status)
            WHERE indexing_status = 'Pending';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_medical_cases_indexing_status_pending"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_medical_cases_embedding"";");
        migrationBuilder.Sql("ALTER TABLE medical_cases DROP COLUMN IF EXISTS embedding;");
        migrationBuilder.Sql("ALTER TABLE medical_cases DROP COLUMN IF EXISTS indexing_status;");
        migrationBuilder.Sql("ALTER TABLE medical_cases DROP COLUMN IF EXISTS version;");
    }
}

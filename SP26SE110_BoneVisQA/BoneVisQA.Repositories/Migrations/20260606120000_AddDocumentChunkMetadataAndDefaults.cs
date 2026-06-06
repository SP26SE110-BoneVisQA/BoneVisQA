using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

/// <inheritdoc />
public partial class AddDocumentChunkMetadataAndDefaults : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE documents
            ADD COLUMN IF NOT EXISTS default_modality text;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE documents
            ADD COLUMN IF NOT EXISTS default_pathology_group text;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            ADD COLUMN IF NOT EXISTS modality text NOT NULL DEFAULT 'Other';
            """);

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            ADD COLUMN IF NOT EXISTS anatomy text NOT NULL DEFAULT 'Other';
            """);

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            ADD COLUMN IF NOT EXISTS pathology_group text NOT NULL DEFAULT 'Other';
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS ix_document_chunks_modality_anatomy
            ON document_chunks (modality, anatomy);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_document_chunks_modality_anatomy;");

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            DROP COLUMN IF EXISTS pathology_group;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            DROP COLUMN IF EXISTS anatomy;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE document_chunks
            DROP COLUMN IF EXISTS modality;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE documents
            DROP COLUMN IF EXISTS default_pathology_group;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE documents
            DROP COLUMN IF EXISTS default_modality;
            """);
    }
}

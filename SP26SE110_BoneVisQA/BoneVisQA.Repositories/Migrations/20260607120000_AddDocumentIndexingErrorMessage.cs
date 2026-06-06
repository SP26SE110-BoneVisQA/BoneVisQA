using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

/// <inheritdoc />
public partial class AddDocumentIndexingErrorMessage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE documents
            ADD COLUMN IF NOT EXISTS indexing_error_message text;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE documents
            DROP COLUMN IF EXISTS indexing_error_message;
            """);
    }
}

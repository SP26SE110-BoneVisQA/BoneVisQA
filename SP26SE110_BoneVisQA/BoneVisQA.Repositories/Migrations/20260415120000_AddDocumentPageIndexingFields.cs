using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <summary>Adds page-level indexing progress fields for PDF RAG ingestion.</summary>
    public class AddDocumentPageIndexingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "total_pages",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_page_indexing",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "total_pages",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "current_page_indexing",
                table: "documents");
        }
    }
}

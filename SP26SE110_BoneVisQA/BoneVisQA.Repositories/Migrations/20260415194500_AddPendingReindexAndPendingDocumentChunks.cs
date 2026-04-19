using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    public partial class AddPendingReindexAndPendingDocumentChunks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pending_reindex_hash",
                table: "documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_reindex_path",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pending_document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pending_document_chunks_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "pending_document_chunks_doc_id_chunk_order_key",
                table: "pending_document_chunks",
                columns: new[] { "doc_id", "chunk_order" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_document_chunks");

            migrationBuilder.DropColumn(
                name: "pending_reindex_hash",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "pending_reindex_path",
                table: "documents");
        }
    }
}

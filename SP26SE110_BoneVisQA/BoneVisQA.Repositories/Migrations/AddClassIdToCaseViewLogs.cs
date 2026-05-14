using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddClassIdToCaseViewLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "class_id",
                table: "case_view_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_case_view_logs_class_id",
                table: "case_view_logs",
                column: "class_id");

            migrationBuilder.AddForeignKey(
                name: "case_view_logs_class_id_fkey",
                table: "case_view_logs",
                column: "class_id",
                principalTable: "academic_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "case_view_logs_class_id_fkey",
                table: "case_view_logs");

            migrationBuilder.DropIndex(
                name: "idx_case_view_logs_class_id",
                table: "case_view_logs");

            migrationBuilder.DropColumn(
                name: "class_id",
                table: "case_view_logs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddTriageWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "case_answers",
                type: "text",
                nullable: false,
                defaultValueSql: "'Pending'::text",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldDefaultValueSql: "'Pending'::text");

            migrationBuilder.AddColumn<double>(
                name: "ai_confidence_score",
                table: "case_answers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "escalated_at",
                table: "case_answers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "escalated_by_id",
                table: "case_answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "expert_id",
                table: "academic_classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_answers_escalated_by_id",
                table: "case_answers",
                column: "escalated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_classes_expert_id",
                table: "academic_classes",
                column: "expert_id");

            migrationBuilder.AddForeignKey(
                name: "academic_classes_expert_id_fkey",
                table: "academic_classes",
                column: "expert_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "case_answers_escalated_by_id_fkey",
                table: "case_answers",
                column: "escalated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "academic_classes_expert_id_fkey",
                table: "academic_classes");

            migrationBuilder.DropForeignKey(
                name: "case_answers_escalated_by_id_fkey",
                table: "case_answers");

            migrationBuilder.DropIndex(
                name: "IX_case_answers_escalated_by_id",
                table: "case_answers");

            migrationBuilder.DropIndex(
                name: "IX_academic_classes_expert_id",
                table: "academic_classes");

            migrationBuilder.DropColumn(
                name: "ai_confidence_score",
                table: "case_answers");

            migrationBuilder.DropColumn(
                name: "escalated_at",
                table: "case_answers");

            migrationBuilder.DropColumn(
                name: "escalated_by_id",
                table: "case_answers");

            migrationBuilder.DropColumn(
                name: "expert_id",
                table: "academic_classes");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "case_answers",
                type: "text",
                nullable: true,
                defaultValueSql: "'Pending'::text",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValueSql: "'Pending'::text");
        }
    }
}

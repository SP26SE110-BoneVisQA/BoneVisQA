using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class MedicalCasePromotionAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "review_version",
                table: "medical_cases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validated_at",
                table: "medical_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validated_by",
                table: "medical_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_validated_by",
                table: "medical_cases",
                column: "validated_by");

            migrationBuilder.AddForeignKey(
                name: "medical_cases_validated_by_fkey",
                table: "medical_cases",
                column: "validated_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "medical_cases_validated_by_fkey",
                table: "medical_cases");

            migrationBuilder.DropIndex(
                name: "IX_medical_cases_validated_by",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "review_version",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "validated_at",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "validated_by",
                table: "medical_cases");
        }
    }
}

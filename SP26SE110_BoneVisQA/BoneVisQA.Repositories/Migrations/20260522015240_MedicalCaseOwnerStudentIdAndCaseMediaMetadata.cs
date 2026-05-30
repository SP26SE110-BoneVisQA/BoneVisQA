using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Maps existing Supabase tables <c>case_metadata</c> and <c>case_media</c> in EF only.
    /// This migration adds <c>owner_student_id</c>; do not recreate ontology tables if they already exist.
    /// </remarks>
    public partial class MedicalCaseOwnerStudentIdAndCaseMediaMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_student_id",
                table: "medical_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_medical_cases_owner_student_id",
                table: "medical_cases",
                column: "owner_student_id");

            migrationBuilder.AddForeignKey(
                name: "medical_cases_owner_student_id_fkey",
                table: "medical_cases",
                column: "owner_student_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "medical_cases_owner_student_id_fkey",
                table: "medical_cases");

            migrationBuilder.DropIndex(
                name: "idx_medical_cases_owner_student_id",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "owner_student_id",
                table: "medical_cases");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

/// <summary>Adds bone_specialties, expert_specialties, and academic_classes.class_specialty_id.</summary>
public class AddBoneSpecialtiesAndClassSpecialty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bone_specialties",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("bone_specialties_pkey", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "expert_specialties",
            columns: table => new
            {
                expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("expert_specialties_pkey", x => new { x.expert_id, x.bone_specialty_id });
                table.ForeignKey(
                    name: "expert_specialties_bone_specialty_id_fkey",
                    column: x => x.bone_specialty_id,
                    principalTable: "bone_specialties",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "expert_specialties_expert_id_fkey",
                    column: x => x.expert_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "bone_specialties_name_key",
            table: "bone_specialties",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_expert_specialties_bone_specialty_id",
            table: "expert_specialties",
            column: "bone_specialty_id");

        migrationBuilder.AddColumn<Guid>(
            name: "class_specialty_id",
            table: "academic_classes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_academic_classes_class_specialty_id",
            table: "academic_classes",
            column: "class_specialty_id");

        migrationBuilder.AddForeignKey(
            name: "academic_classes_class_specialty_id_fkey",
            table: "academic_classes",
            column: "class_specialty_id",
            principalTable: "bone_specialties",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "academic_classes_class_specialty_id_fkey",
            table: "academic_classes");

        migrationBuilder.DropIndex(
            name: "IX_academic_classes_class_specialty_id",
            table: "academic_classes");

        migrationBuilder.DropColumn(
            name: "class_specialty_id",
            table: "academic_classes");

        migrationBuilder.DropTable(name: "expert_specialties");

        migrationBuilder.DropTable(name: "bone_specialties");
    }
}

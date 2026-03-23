using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    public partial class AddGoogleAuthFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_id",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_url",
                table: "users",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "google_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_url",
                table: "users");
        }
    }
}

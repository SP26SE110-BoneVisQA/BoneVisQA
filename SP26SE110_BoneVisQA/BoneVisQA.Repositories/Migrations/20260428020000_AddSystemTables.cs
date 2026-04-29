using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

public partial class AddSystemTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "system_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                user_email = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("system_logs_pkey", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "system_configs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                config_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                config_value = table.Column<string>(type: "text", nullable: false),
                category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                value_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("system_configs_pkey", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "backups",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                file_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("backups_pkey", x => x.id);
                table.ForeignKey(
                    name: "backups_created_by_fkey",
                    column: x => x.created_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "data_exports",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                export_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                record_count = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                file_path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("data_exports_pkey", x => x.id);
                table.ForeignKey(
                    name: "data_exports_created_by_fkey",
                    column: x => x.created_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "ix_system_logs_timestamp",
            table: "system_logs",
            column: "timestamp");

        migrationBuilder.CreateIndex(
            name: "ix_system_logs_level",
            table: "system_logs",
            column: "level");

        migrationBuilder.CreateIndex(
            name: "ix_system_logs_category",
            table: "system_logs",
            column: "category");

        migrationBuilder.CreateIndex(
            name: "ix_system_configs_key",
            table: "system_configs",
            column: "config_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_system_configs_category",
            table: "system_configs",
            column: "category");

        migrationBuilder.CreateIndex(
            name: "ix_backups_status",
            table: "backups",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_backups_created_at",
            table: "backups",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_data_exports_status",
            table: "data_exports",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_data_exports_created_at",
            table: "data_exports",
            column: "created_at");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "system_logs");
        migrationBuilder.DropTable(name: "system_configs");
        migrationBuilder.DropTable(name: "backups");
        migrationBuilder.DropTable(name: "data_exports");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualQaSpecialtyRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE users
                ADD COLUMN IF NOT EXISTS primary_bone_specialty_id uuid;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE visual_qa_sessions
                ADD COLUMN IF NOT EXISTS target_bone_specialty_id uuid;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_users_primary_bone_specialty_id
                ON users (primary_bone_specialty_id);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_target_bone_specialty
                ON visual_qa_sessions (target_bone_specialty_id);
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'users_primary_bone_specialty_id_fkey'
                    ) THEN
                        ALTER TABLE users
                        ADD CONSTRAINT users_primary_bone_specialty_id_fkey
                        FOREIGN KEY (primary_bone_specialty_id)
                        REFERENCES bone_specialties (id)
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'visual_qa_sessions_target_bone_specialty_id_fkey'
                    ) THEN
                        ALTER TABLE visual_qa_sessions
                        ADD CONSTRAINT visual_qa_sessions_target_bone_specialty_id_fkey
                        FOREIGN KEY (target_bone_specialty_id)
                        REFERENCES bone_specialties (id)
                        ON DELETE SET NULL;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE users
                DROP CONSTRAINT IF EXISTS users_primary_bone_specialty_id_fkey;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE visual_qa_sessions
                DROP CONSTRAINT IF EXISTS visual_qa_sessions_target_bone_specialty_id_fkey;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_visual_qa_sessions_target_bone_specialty;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS idx_users_primary_bone_specialty_id;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE visual_qa_sessions
                DROP COLUMN IF EXISTS target_bone_specialty_id;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE users
                DROP COLUMN IF EXISTS primary_bone_specialty_id;
                """);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class FixQuizQuestionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:auth.aal_level", "aal1,aal2,aal3")
                .Annotation("Npgsql:Enum:auth.code_challenge_method", "s256,plain")
                .Annotation("Npgsql:Enum:auth.factor_status", "unverified,verified")
                .Annotation("Npgsql:Enum:auth.factor_type", "totp,webauthn,phone")
                .Annotation("Npgsql:Enum:auth.oauth_authorization_status", "pending,approved,denied,expired")
                .Annotation("Npgsql:Enum:auth.oauth_client_type", "public,confidential")
                .Annotation("Npgsql:Enum:auth.oauth_registration_type", "dynamic,manual")
                .Annotation("Npgsql:Enum:auth.oauth_response_type", "code")
                .Annotation("Npgsql:Enum:auth.one_time_token_type", "confirmation_token,reauthentication_token,recovery_token,email_change_token_new,email_change_token_current,phone_change_token")
                .Annotation("Npgsql:Enum:realtime.action", "INSERT,UPDATE,DELETE,TRUNCATE,ERROR")
                .Annotation("Npgsql:Enum:realtime.equality_op", "eq,neq,lt,lte,gt,gte,in")
                .Annotation("Npgsql:Enum:storage.buckettype", "STANDARD,ANALYTICS,VECTOR")
                .Annotation("Npgsql:PostgresExtension:extensions.pg_stat_statements", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:extensions.uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:graphql.pg_graphql", ",,")
                .Annotation("Npgsql:PostgresExtension:vault.supabase_vault", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "bone_specialties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("bone_specialties_pkey", x => x.id);
                    table.ForeignKey(
                        name: "bone_specialties_parent_id_fkey",
                        column: x => x.parent_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("categories_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pending_document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    start_page = table.Column<int>(type: "integer", nullable: false),
                    end_page = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pending_document_chunks_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "question_trends",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "bone_specialty"),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    trend_direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "stable"),
                    change_percentage = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m),
                    period_start = table.Column<DateOnly>(type: "date", nullable: true),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("question_trends_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("roles_pkey", x => x.id);
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
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tags_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pathology_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pathology_categories_pkey", x => x.id);
                    table.ForeignKey(
                        name: "pathology_categories_bone_specialty_id_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: true),
                    school_cohort = table.Column<string>(type: "text", nullable: true),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    google_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    department = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    specialty = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    primary_bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    gender = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    student_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    class_code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    emergency_contact = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    medical_school = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    medical_student_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    verification_notes = table.Column<string>(type: "text", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                    table.ForeignKey(
                        name: "users_primary_specialty_fkey",
                        column: x => x.primary_bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "users_verified_by_fkey",
                        column: x => x.verified_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    pending_reindex_path = table.Column<string>(type: "text", nullable: true),
                    pending_reindex_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "1.0.0"),
                    pending_target_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    total_pages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_page_indexing = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_chunks = table.Column<int>(type: "integer", nullable: false),
                    is_outdated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    indexing_status = table.Column<string>(type: "text", nullable: false),
                    indexing_progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("documents_pkey", x => x.id);
                    table.ForeignKey(
                        name: "documents_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "competency_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pathology_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mastery_thresholds = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("competency_definitions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_competency_definitions_bone_specialties_bone_specialty_id",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_competency_definitions_pathology_categories_pathology_categ~",
                        column: x => x.pathology_category_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "academic_classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_name = table.Column<string>(type: "text", nullable: false),
                    semester = table.Column<string>(type: "text", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    class_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    focus_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "'Basic'::text"),
                    teaching_objectives = table.Column<string>(type: "jsonb", nullable: true),
                    target_pathology_categories = table.Column<string>(type: "jsonb", nullable: true),
                    target_student_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("academic_classes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "academic_classes_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "academic_classes_lecturer_id_fkey",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "academic_classes_specialty_fkey",
                        column: x => x.class_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateTable(
                name: "error_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_pattern = table.Column<string>(type: "text", nullable: true),
                    error_topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    topic_hint = table.Column<string>(type: "text", nullable: true),
                    first_occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    last_occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("error_patterns_pkey", x => x.id);
                    table.ForeignKey(
                        name: "error_patterns_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expert_specialties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pathology_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proficiency_level = table.Column<int>(type: "integer", nullable: false),
                    years_experience = table.Column<int>(type: "integer", nullable: true),
                    certifications = table.Column<string>(type: "jsonb", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("expert_specialties_pkey", x => x.id);
                    table.ForeignKey(
                        name: "expert_specialties_bone_specialty_id_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "expert_specialties_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "expert_specialties_pathology_category_id_fkey",
                        column: x => x.pathology_category_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "flashcard_decks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    deck_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("flashcard_decks_pkey", x => x.id);
                    table.ForeignKey(
                        name: "flashcard_decks_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_insights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    insight_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    related_bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_pathology_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recommended_action = table.Column<string>(type: "text", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    is_action_taken = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("learning_insights_pkey", x => x.id);
                    table.ForeignKey(
                        name: "learning_insights_bone_specialty_id_fkey",
                        column: x => x.related_bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "learning_insights_pathology_id_fkey",
                        column: x => x.related_pathology_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "learning_insights_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medical_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    is_approved = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    suggested_diagnosis = table.Column<string>(type: "text", nullable: true),
                    key_findings = table.Column<string>(type: "text", nullable: true),
                    reflective_questions = table.Column<string>(type: "text", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    indexing_status = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, defaultValue: "1.0.0"),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pathology_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    patient_age_group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    emergency_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    body_region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medical_cases_pkey", x => x.id);
                    table.ForeignKey(
                        name: "medical_cases_assigned_expert_id_fkey",
                        column: x => x.assigned_expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "medical_cases_bone_specialty_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "medical_cases_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "medical_cases_created_by_expert_id_fkey",
                        column: x => x.created_by_expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "medical_cases_pathology_fkey",
                        column: x => x.pathology_category_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    target_url = table.Column<string>(type: "text", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("notifications_pkey", x => x.id);
                    table.ForeignKey(
                        name: "notifications_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("password_reset_tokens_pkey", x => x.id);
                    table.ForeignKey(
                        name: "password_reset_tokens_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quizzes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    open_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    close_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_limit = table.Column<int>(type: "integer", nullable: true),
                    passing_score = table.Column<int>(type: "integer", nullable: true),
                    created_by_expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    topic = table.Column<string>(type: "text", nullable: true),
                    is_ai_generated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    difficulty = table.Column<string>(type: "text", nullable: true),
                    classification = table.Column<string>(type: "text", nullable: true),
                    is_verified_curriculum = table.Column<bool>(type: "boolean", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: true, defaultValue: "'multiple_choice'::text"),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pathology_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teaching_points = table.Column<int>(type: "integer", nullable: true),
                    learning_objectives = table.Column<string>(type: "jsonb", nullable: true),
                    target_student_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    adaptive_difficulty = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    spaced_repetition_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    quiz_mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("quizzes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_quizzes_users_created_by_lecturer_id",
                        column: x => x.created_by_lecturer_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "quizzes_assigned_by_expert_id_fkey",
                        column: x => x.assigned_expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "quizzes_bone_specialty_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "quizzes_created_by_expert_id_fkey",
                        column: x => x.created_by_expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "quizzes_pathology_fkey",
                        column: x => x.pathology_category_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "student_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pathology_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score = table.Column<decimal>(type: "numeric", nullable: false),
                    total_attempts = table.Column<int>(type: "integer", nullable: false),
                    correct_attempts = table.Column<int>(type: "integer", nullable: false),
                    mastery_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_competencies_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_competencies_bone_specialty_id_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_competencies_pathology_id_fkey",
                        column: x => x.pathology_category_id,
                        principalTable: "pathology_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_competencies_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_roles_pkey", x => x.id);
                    table.ForeignKey(
                        name: "user_roles_role_id_fkey",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_roles_user_id_fkey",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    chunk_order = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false),
                    flagged_by_expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    flag_reason = table.Column<string>(type: "text", nullable: true),
                    flagged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    start_page = table.Column<int>(type: "integer", nullable: true),
                    end_page = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("document_chunks_pkey", x => x.id);
                    table.ForeignKey(
                        name: "document_chunks_doc_id_fkey",
                        column: x => x.doc_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "document_chunks_flagged_by_expert_id_fkey",
                        column: x => x.flagged_by_expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "document_tags",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_tags", x => new { x.document_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_document_tags_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    send_email = table.Column<bool>(type: "boolean", nullable: true, defaultValueSql: "true"),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("announcements_pkey", x => x.id);
                    table.ForeignKey(
                        name: "announcements_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_name = table.Column<string>(type: "text", nullable: true),
                    enrolled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_enrollments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "class_enrollments_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_enrollments_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_tags",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_tags_pkey", x => new { x.class_id, x.tag_id });
                    table.ForeignKey(
                        name: "class_tags_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_tags_tag_id_fkey",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "learning_statistics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_cases_viewed = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    total_questions_asked = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    avg_quiz_score = table.Column<double>(type: "double precision", nullable: true),
                    error_distribution = table.Column<string>(type: "jsonb", nullable: true),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("learning_statistics_pkey", x => x.id);
                    table.ForeignKey(
                        name: "learning_statistics_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "learning_statistics_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teaching_objective_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    objective = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("teaching_objective_suggestions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "teaching_objective_suggestions_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "teaching_objective_suggestions_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "teaching_objective_suggestions_reviewer_id_fkey",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "class_expert_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bone_specialty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_in_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpertSpecialtyId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_expert_assignments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_expert_assignments_expert_specialties_ExpertSpecialty~",
                        column: x => x.ExpertSpecialtyId,
                        principalTable: "expert_specialties",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "class_expert_assignments_bone_specialty_id_fkey",
                        column: x => x.bone_specialty_id,
                        principalTable: "bone_specialties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "class_expert_assignments_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_expert_assignments_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flashcards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false),
                    front_content = table.Column<string>(type: "text", nullable: false),
                    back_content = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ease_factor = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 2.5m),
                    interval_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    repetition_count = table.Column<int>(type: "integer", nullable: false),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("flashcards_pkey", x => x.id);
                    table.ForeignKey(
                        name: "flashcards_deck_id_fkey",
                        column: x => x.deck_id,
                        principalTable: "flashcard_decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_tags",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("case_tags_pkey", x => new { x.case_id, x.tag_id });
                    table.ForeignKey(
                        name: "case_tags_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "case_tags_tag_id_fkey",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_view_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("case_view_logs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "case_view_logs_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "case_view_logs_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "case_view_logs_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "medical_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    modality = table.Column<string>(type: "text", nullable: true),
                    view_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    body_part = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contrast_used = table.Column<bool>(type: "boolean", nullable: true),
                    image_quality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    clinical_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("medical_images_pkey", x => x.id);
                    table.ForeignKey(
                        name: "medical_images_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    difficulty_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    ease_factor = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 2.5m),
                    review_interval = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    current_question_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("quiz_attempts_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quiz_attempts_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "quiz_attempts_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "int", nullable: true),
                    option_a = table.Column<string>(type: "text", nullable: true),
                    option_b = table.Column<string>(type: "text", nullable: true),
                    option_c = table.Column<string>(type: "text", nullable: true),
                    option_d = table.Column<string>(type: "text", nullable: true),
                    correct_answer = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    reference_answer = table.Column<string>(type: "text", nullable: true),
                    max_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    hint = table.Column<string>(type: "text", nullable: true),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    correct_answers = table.Column<string>(type: "jsonb", nullable: true),
                    accepted_answers = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("quiz_questions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quiz_questions_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "quiz_questions_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_cases",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_cases_pkey", x => new { x.class_id, x.case_id });
                    table.ForeignKey(
                        name: "FK_class_cases_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "class_cases_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_cases_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_quiz_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    open_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    close_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    passing_score = table.Column<int>(type: "integer", nullable: true),
                    time_limit_minutes = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    shuffle_questions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_retake = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_late = table.Column<bool>(type: "boolean", nullable: false),
                    show_results_after_submission = table.Column<bool>(type: "boolean", nullable: false),
                    retake_reset_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_answers_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shuffle_options = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    quiz_mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("class_quiz_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_quiz_sessions_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalTable: "announcements",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "class_quiz_sessions_class_id_fkey",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "class_quiz_sessions_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_annotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    image_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coordinates = table.Column<string>(type: "jsonb", nullable: true),
                    label = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("case_annotations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "case_annotations_image_id_fkey",
                        column: x => x.image_id,
                        principalTable: "medical_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visual_qa_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_image_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Active"),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    promoted_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_review_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("visual_qa_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "FK_visual_qa_sessions_medical_cases_promoted_case_id",
                        column: x => x.promoted_case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "visual_qa_sessions_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "visual_qa_sessions_image_id_fkey",
                        column: x => x.image_id,
                        principalTable: "medical_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "visual_qa_sessions_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quiz_review_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    student_answer = table.Column<string>(type: "text", nullable: true),
                    correct_answer = table.Column<string>(type: "text", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    related_cases = table.Column<string>(type: "jsonb", nullable: false),
                    topic_tags = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("quiz_review_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "quiz_review_items_attempt_id_fkey",
                        column: x => x.attempt_id,
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "quiz_review_items_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: false),
                    ease_factor = table.Column<decimal>(type: "numeric", nullable: false),
                    interval_days = table.Column<int>(type: "integer", nullable: false),
                    repetition_count = table.Column<int>(type: "integer", nullable: false),
                    last_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_quality = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("review_schedules_pkey", x => x.id);
                    table.ForeignKey(
                        name: "review_schedules_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "review_schedules_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "review_schedules_quiz_id_fkey",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "review_schedules_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_quiz_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_answer = table.Column<string>(type: "text", nullable: true),
                    essay_answer = table.Column<string>(type: "text", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    score_awarded = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    lecturer_feedback = table.Column<string>(type: "text", nullable: true),
                    graded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    graded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_graded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_quiz_answers_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_quiz_answers_attempt_id_fkey",
                        column: x => x.attempt_id,
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "student_quiz_answers_graded_by_fkey",
                        column: x => x.graded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_quiz_answers_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    annotation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: true),
                    custom_image_url = table.Column<string>(type: "text", nullable: true),
                    custom_coordinates = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("student_questions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "student_questions_annotation_id_fkey",
                        column: x => x.annotation_id,
                        principalTable: "case_annotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "student_questions_case_id_fkey",
                        column: x => x.case_id,
                        principalTable: "medical_cases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "student_questions_student_id_fkey",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qa_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    coordinates = table.Column<string>(type: "jsonb", nullable: true),
                    target_assistant_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    citations_json = table.Column<string>(type: "jsonb", nullable: true),
                    suggested_diagnosis = table.Column<string>(type: "text", nullable: true),
                    differential_diagnoses = table.Column<string>(type: "jsonb", nullable: true),
                    key_imaging_findings = table.Column<string>(type: "text", nullable: true),
                    reflective_questions = table.Column<string>(type: "text", nullable: true),
                    ai_confidence_score = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("qa_messages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "qa_messages_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "visual_qa_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: true),
                    structured_diagnosis = table.Column<string>(type: "text", nullable: true),
                    differential_diagnoses = table.Column<string>(type: "text", nullable: true),
                    key_imaging_findings = table.Column<string>(type: "text", nullable: true),
                    reflective_questions = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValueSql: "'Pending'::text"),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escalated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    escalated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ai_confidence_score = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("case_answers_pkey", x => x.id);
                    table.CheckConstraint("case_answers_status_check", "status = ANY (ARRAY['Pending'::text, 'RequiresLecturerReview'::text, 'Approved'::text, 'Edited'::text, 'Rejected'::text, 'Escalated'::text, 'EscalatedToExpert'::text, 'ExpertApproved'::text, 'Revised'::text])");
                    table.ForeignKey(
                        name: "case_answers_escalated_by_id_fkey",
                        column: x => x.escalated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "case_answers_question_id_fkey",
                        column: x => x.question_id,
                        principalTable: "student_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "case_answers_reviewed_by_id_fkey",
                        column: x => x.reviewed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "citations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chunk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    similarity_score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("citations_pkey", x => x.id);
                    table.ForeignKey(
                        name: "citations_answer_id_fkey",
                        column: x => x.answer_id,
                        principalTable: "case_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "citations_chunk_id_fkey",
                        column: x => x.chunk_id,
                        principalTable: "document_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "citations_message_id_fkey",
                        column: x => x.message_id,
                        principalTable: "qa_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expert_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "text", nullable: true),
                    corrected_roi = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("expert_reviews_pkey", x => x.id);
                    table.ForeignKey(
                        name: "expert_reviews_answer_id_fkey",
                        column: x => x.answer_id,
                        principalTable: "case_answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "expert_reviews_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "expert_reviews_session_id_fkey",
                        column: x => x.session_id,
                        principalTable: "visual_qa_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_classes_class_specialty_id",
                table: "academic_classes",
                column: "class_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_classes_expert_id",
                table: "academic_classes",
                column: "expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_classes_lecturer_id",
                table: "academic_classes",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "idx_announcements_class",
                table: "announcements",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_backups_created_at",
                table: "backups",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_backups_created_by",
                table: "backups",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_backups_status",
                table: "backups",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_bone_specialties_code",
                table: "bone_specialties",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_bone_specialties_parent",
                table: "bone_specialties",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_annotations_image",
                table: "case_annotations",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_answers_question",
                table: "case_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_answers_escalated_by_id",
                table: "case_answers",
                column: "escalated_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_answers_reviewed_by_id",
                table: "case_answers",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_tags_case",
                table: "case_tags",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_tags_tag",
                table: "case_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_view_logs_student_case",
                table: "case_view_logs",
                columns: new[] { "student_id", "case_id" });

            migrationBuilder.CreateIndex(
                name: "IX_case_view_logs_case_id",
                table: "case_view_logs",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_view_logs_class_id",
                table: "case_view_logs",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "categories_name_key",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_citations_answer",
                table: "citations",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "idx_citations_message",
                table: "citations",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_citations_chunk_id",
                table: "citations",
                column: "chunk_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_cases_announcement_id",
                table: "class_cases",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_cases_case_id",
                table: "class_cases",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "class_enrollments_class_id_student_id_key",
                table: "class_enrollments",
                columns: new[] { "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_class_enrollments_student",
                table: "class_enrollments",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "idx_class_expert_assignments_class",
                table: "class_expert_assignments",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_class_expert_assignments_expert",
                table: "class_expert_assignments",
                column: "expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_expert_assignments_bone_specialty_id",
                table: "class_expert_assignments",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_expert_assignments_ExpertSpecialtyId",
                table: "class_expert_assignments",
                column: "ExpertSpecialtyId");

            migrationBuilder.CreateIndex(
                name: "class_quiz_sessions_class_id_quiz_id_key",
                table: "class_quiz_sessions",
                columns: new[] { "class_id", "quiz_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_quiz_sessions_announcement_id",
                table: "class_quiz_sessions",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_quiz_sessions_quiz_id",
                table: "class_quiz_sessions",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "idx_class_tags_class",
                table: "class_tags",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_class_tags_tag",
                table: "class_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "competency_definitions_unique",
                table: "competency_definitions",
                columns: new[] { "bone_specialty_id", "pathology_category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_competency_definitions_pathology_category_id",
                table: "competency_definitions",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_created_at",
                table: "data_exports",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_exports_created_by",
                table: "data_exports",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_data_exports_status",
                table: "data_exports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "document_chunks_doc_id_chunk_order_key",
                table: "document_chunks",
                columns: new[] { "doc_id", "chunk_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_chunks_doc_id",
                table: "document_chunks",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_embedding",
                table: "document_chunks",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_flagged_by_expert_id",
                table: "document_chunks",
                column: "flagged_by_expert_id");

            migrationBuilder.CreateIndex(
                name: "idx_document_tags_document",
                table: "document_tags",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "idx_document_tags_tag",
                table: "document_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_category_id",
                table: "documents",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_documents_content_hash",
                table: "documents",
                column: "content_hash",
                unique: true,
                filter: "\"content_hash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_error_patterns_student_id",
                table: "error_patterns",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "expert_reviews_expert_id_answer_id_key",
                table: "expert_reviews",
                columns: new[] { "expert_id", "answer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "expert_reviews_expert_id_session_id_key",
                table: "expert_reviews",
                columns: new[] { "expert_id", "session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expert_reviews_answer_id",
                table: "expert_reviews",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_expert_reviews_session_id",
                table: "expert_reviews",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_specialties_bone",
                table: "expert_specialties",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "idx_expert_specialties_expert",
                table: "expert_specialties",
                column: "expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_expert_specialties_pathology_category_id",
                table: "expert_specialties",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_flashcard_decks_student_id",
                table: "flashcard_decks",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_flashcards_deck_id",
                table: "flashcards",
                column: "deck_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_insights_related_bone_specialty_id",
                table: "learning_insights",
                column: "related_bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_insights_related_pathology_id",
                table: "learning_insights",
                column: "related_pathology_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_insights_student_id",
                table: "learning_insights",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_learning_statistics_class_id",
                table: "learning_statistics",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "learning_statistics_student_id_class_id_key",
                table: "learning_statistics",
                columns: new[] { "student_id", "class_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_assigned_expert_id",
                table: "medical_cases",
                column: "assigned_expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_bone_specialty_id",
                table: "medical_cases",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_category_id",
                table: "medical_cases",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_created_by_expert_id",
                table: "medical_cases",
                column: "created_by_expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_embedding",
                table: "medical_cases",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_pathology_category_id",
                table: "medical_cases",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "idx_medical_images_case",
                table: "medical_images",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_password_reset_tokens_token",
                table: "password_reset_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_pathology_categories_bone",
                table: "pathology_categories",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "idx_pathology_categories_code",
                table: "pathology_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "pending_document_chunks_doc_id_chunk_order_key",
                table: "pending_document_chunks",
                columns: new[] { "doc_id", "chunk_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_qa_messages_role",
                table: "qa_messages",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "idx_qa_messages_session_created_at",
                table: "qa_messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_qa_messages_session_client_request_role",
                table: "qa_messages",
                columns: new[] { "session_id", "client_request_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_quiz_attempts_student_quiz",
                table: "quiz_attempts",
                columns: new[] { "student_id", "quiz_id" });

            migrationBuilder.CreateIndex(
                name: "IX_quiz_attempts_quiz_id",
                table: "quiz_attempts",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "quiz_attempts_student_id_quiz_id_key",
                table: "quiz_attempts",
                columns: new[] { "student_id", "quiz_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_case_id",
                table: "quiz_questions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_questions_quiz_id",
                table: "quiz_questions",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_review_items_attempt_id",
                table: "quiz_review_items",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_review_items_question_id",
                table: "quiz_review_items",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_assigned_expert_id",
                table: "quizzes",
                column: "assigned_expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_bone_specialty_id",
                table: "quizzes",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_created_by_expert_id",
                table: "quizzes",
                column: "created_by_expert_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_created_by_lecturer_id",
                table: "quizzes",
                column: "created_by_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_pathology_category_id",
                table: "quizzes",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_case_id",
                table: "review_schedules",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_question_id",
                table: "review_schedules",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_quiz_id",
                table: "review_schedules",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_student_id",
                table: "review_schedules",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "roles_name_key",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_competencies_bone_specialty_id",
                table: "student_competencies",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_competencies_pathology_category_id",
                table: "student_competencies",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "student_competencies_unique",
                table: "student_competencies",
                columns: new[] { "student_id", "bone_specialty_id", "pathology_category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_student_questions_case",
                table: "student_questions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "idx_student_questions_student",
                table: "student_questions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_questions_annotation_id",
                table: "student_questions",
                column: "annotation_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_quiz_answers_graded_by",
                table: "student_quiz_answers",
                column: "graded_by");

            migrationBuilder.CreateIndex(
                name: "IX_student_quiz_answers_question_id",
                table: "student_quiz_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "student_quiz_answers_attempt_id_question_id_key",
                table: "student_quiz_answers",
                columns: new[] { "attempt_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_category",
                table: "system_configs",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_system_configs_key",
                table: "system_configs",
                column: "config_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_system_logs_category",
                table: "system_logs",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_system_logs_level",
                table: "system_logs",
                column: "level");

            migrationBuilder.CreateIndex(
                name: "ix_system_logs_timestamp",
                table: "system_logs",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "idx_tags_type",
                table: "tags",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "tags_name_type_key",
                table: "tags",
                columns: new[] { "name", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_teaching_objective_suggestions_class",
                table: "teaching_objective_suggestions",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_teaching_objective_suggestions_expert",
                table: "teaching_objective_suggestions",
                column: "expert_id");

            migrationBuilder.CreateIndex(
                name: "idx_teaching_objective_suggestions_status",
                table: "teaching_objective_suggestions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_teaching_objective_suggestions_reviewed_by",
                table: "teaching_objective_suggestions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_role",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_roles_user",
                table: "user_roles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "user_roles_user_id_role_id_key",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_users_is_active",
                table: "users",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_users_primary_bone_specialty_id",
                table: "users",
                column: "primary_bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_verified_by",
                table: "users",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_visual_qa_sessions_case",
                table: "visual_qa_sessions",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "idx_visual_qa_sessions_status",
                table: "visual_qa_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_visual_qa_sessions_student",
                table: "visual_qa_sessions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_visual_qa_sessions_image_id",
                table: "visual_qa_sessions",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "IX_visual_qa_sessions_promoted_case_id",
                table: "visual_qa_sessions",
                column: "promoted_case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backups");

            migrationBuilder.DropTable(
                name: "case_tags");

            migrationBuilder.DropTable(
                name: "case_view_logs");

            migrationBuilder.DropTable(
                name: "citations");

            migrationBuilder.DropTable(
                name: "class_cases");

            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "class_expert_assignments");

            migrationBuilder.DropTable(
                name: "class_quiz_sessions");

            migrationBuilder.DropTable(
                name: "class_tags");

            migrationBuilder.DropTable(
                name: "competency_definitions");

            migrationBuilder.DropTable(
                name: "data_exports");

            migrationBuilder.DropTable(
                name: "document_tags");

            migrationBuilder.DropTable(
                name: "error_patterns");

            migrationBuilder.DropTable(
                name: "expert_reviews");

            migrationBuilder.DropTable(
                name: "flashcards");

            migrationBuilder.DropTable(
                name: "learning_insights");

            migrationBuilder.DropTable(
                name: "learning_statistics");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "pending_document_chunks");

            migrationBuilder.DropTable(
                name: "question_trends");

            migrationBuilder.DropTable(
                name: "quiz_review_items");

            migrationBuilder.DropTable(
                name: "review_schedules");

            migrationBuilder.DropTable(
                name: "student_competencies");

            migrationBuilder.DropTable(
                name: "student_quiz_answers");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "teaching_objective_suggestions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "qa_messages");

            migrationBuilder.DropTable(
                name: "expert_specialties");

            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "case_answers");

            migrationBuilder.DropTable(
                name: "flashcard_decks");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "quiz_questions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "visual_qa_sessions");

            migrationBuilder.DropTable(
                name: "academic_classes");

            migrationBuilder.DropTable(
                name: "student_questions");

            migrationBuilder.DropTable(
                name: "quizzes");

            migrationBuilder.DropTable(
                name: "case_annotations");

            migrationBuilder.DropTable(
                name: "medical_images");

            migrationBuilder.DropTable(
                name: "medical_cases");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "pathology_categories");

            migrationBuilder.DropTable(
                name: "bone_specialties");
        }
    }
}

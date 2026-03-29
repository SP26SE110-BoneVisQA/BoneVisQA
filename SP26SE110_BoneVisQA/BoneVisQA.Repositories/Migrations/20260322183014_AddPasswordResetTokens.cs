using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokens : Migration
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
                name: "quizzes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    open_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    close_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_limit = table.Column<int>(type: "integer", nullable: true),
                    passing_score = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("quizzes_pkey", x => x.id);
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_outdated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    indexing_status = table.Column<string>(type: "text", nullable: false)
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
                name: "medical_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    is_approved = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    suggested_diagnosis = table.Column<string>(type: "text", nullable: true),
                    key_findings = table.Column<string>(type: "text", nullable: true),
                    reflective_questions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("medical_cases_pkey", x => x.id);
                    table.ForeignKey(
                        name: "medical_cases_category_id_fkey",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "academic_classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_name = table.Column<string>(type: "text", nullable: false),
                    semester = table.Column<string>(type: "text", nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("academic_classes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "academic_classes_lecturer_id_fkey",
                        column: x => x.lecturer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "quiz_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "quiz_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    question_text = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    option_a = table.Column<string>(type: "text", nullable: true),
                    option_b = table.Column<string>(type: "text", nullable: true),
                    option_c = table.Column<string>(type: "text", nullable: true),
                    option_d = table.Column<string>(type: "text", nullable: true),
                    correct_answer = table.Column<string>(type: "text", nullable: true)
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
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
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
                name: "class_quizzes",
                columns: table => new
                {
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_quizzes", x => new { x.class_id, x.quiz_id });
                    table.ForeignKey(
                        name: "FK_class_quizzes_academic_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "academic_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_quizzes_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
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
                name: "student_quiz_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_answer = table.Column<string>(type: "text", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true)
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
                name: "case_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: true),
                    structured_diagnosis = table.Column<string>(type: "text", nullable: true),
                    differential_diagnoses = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true, defaultValueSql: "'Pending'::text"),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("case_answers_pkey", x => x.id);
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
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "citations_chunk_id_fkey",
                        column: x => x.chunk_id,
                        principalTable: "document_chunks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expert_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "text", nullable: true),
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
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "expert_reviews_expert_id_fkey",
                        column: x => x.expert_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_classes_lecturer_id",
                table: "academic_classes",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "idx_announcements_class",
                table: "announcements",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_annotations_image",
                table: "case_annotations",
                column: "image_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_answers_question",
                table: "case_answers",
                column: "question_id");

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
                name: "categories_name_key",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "citations_answer_id_chunk_id_key",
                table: "citations",
                columns: new[] { "answer_id", "chunk_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_citations_answer",
                table: "citations",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "IX_citations_chunk_id",
                table: "citations",
                column: "chunk_id");

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
                name: "IX_class_quizzes_quiz_id",
                table: "class_quizzes",
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
                name: "document_chunks_doc_id_chunk_order_key",
                table: "document_chunks",
                columns: new[] { "doc_id", "chunk_order" },
                unique: true);

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
                name: "expert_reviews_expert_id_answer_id_key",
                table: "expert_reviews",
                columns: new[] { "expert_id", "answer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expert_reviews_answer_id",
                table: "expert_reviews",
                column: "answer_id");

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
                name: "IX_medical_cases_category_id",
                table: "medical_cases",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_medical_images_case",
                table: "medical_images",
                column: "case_id");

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
                name: "roles_name_key",
                table: "roles",
                column: "name",
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
                name: "IX_student_quiz_answers_question_id",
                table: "student_quiz_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "student_quiz_answers_attempt_id_question_id_key",
                table: "student_quiz_answers",
                columns: new[] { "attempt_id", "question_id" },
                unique: true);

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
                name: "users_email_key",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "case_tags");

            migrationBuilder.DropTable(
                name: "case_view_logs");

            migrationBuilder.DropTable(
                name: "citations");

            migrationBuilder.DropTable(
                name: "class_enrollments");

            migrationBuilder.DropTable(
                name: "class_quizzes");

            migrationBuilder.DropTable(
                name: "class_tags");

            migrationBuilder.DropTable(
                name: "document_tags");

            migrationBuilder.DropTable(
                name: "expert_reviews");

            migrationBuilder.DropTable(
                name: "learning_statistics");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "student_quiz_answers");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "case_answers");

            migrationBuilder.DropTable(
                name: "academic_classes");

            migrationBuilder.DropTable(
                name: "quiz_attempts");

            migrationBuilder.DropTable(
                name: "quiz_questions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "student_questions");

            migrationBuilder.DropTable(
                name: "quizzes");

            migrationBuilder.DropTable(
                name: "case_annotations");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "medical_images");

            migrationBuilder.DropTable(
                name: "medical_cases");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class MergeBeMergeModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "student_questions_case_id_fkey",
                table: "student_questions");

            migrationBuilder.DropIndex(
                name: "bone_specialties_name_key",
                table: "bone_specialties");

            migrationBuilder.RenameIndex(
                name: "IX_case_media_case_id",
                table: "case_media",
                newName: "idx_case_media_case_id");

            migrationBuilder.RenameIndex(
                name: "IX_academic_classes_class_specialty_id",
                table: "academic_classes",
                newName: "idx_academic_classes_class_specialty_id");

            migrationBuilder.AddColumn<string>(
                name: "review_feedback",
                table: "visual_qa_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "time_limit",
                table: "quizzes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "passing_score",
                table: "quizzes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "adaptive_difficulty",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "bone_specialty_id",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_lecturer_id",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "learning_objectives",
                table: "quizzes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pathology_category_id",
                table: "quizzes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quiz_mode",
                table: "quizzes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "spaced_repetition_enabled",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "target_student_level",
                table: "quizzes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "teaching_points",
                table: "quizzes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accepted_answers",
                table: "quiz_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "correct_answers",
                table: "quiz_questions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "explanation",
                table: "quiz_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hint",
                table: "quiz_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_question_index",
                table: "quiz_attempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "difficulty_level",
                table: "quiz_attempts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Medium");

            migrationBuilder.AddColumn<decimal>(
                name: "ease_factor",
                table: "quiz_attempts",
                type: "numeric(4,2)",
                nullable: false,
                defaultValue: 2.5m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "next_review_date",
                table: "quiz_attempts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "review_interval",
                table: "quiz_attempts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "body_part",
                table: "medical_images",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clinical_notes",
                table: "medical_images",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "contrast_used",
                table: "medical_images",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_quality",
                table: "medical_images",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "view_type",
                table: "medical_images",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_region",
                table: "medical_cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bone_specialty_id",
                table: "medical_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_level",
                table: "medical_cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pathology_category_id",
                table: "medical_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patient_age_group",
                table: "medical_cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "medical_cases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "time_limit_minutes",
                table: "class_quiz_sessions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "passing_score",
                table: "class_quiz_sessions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quiz_mode",
                table: "class_quiz_sessions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "release_answers_at",
                table: "class_quiz_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "released_by_id",
                table: "class_quiz_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "shuffle_options",
                table: "class_quiz_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "class_id",
                table: "case_view_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "bone_specialties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "bone_specialties",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "bone_specialties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "bone_specialties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "bone_specialties",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                table: "bone_specialties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "bone_specialties",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "focus_level",
                table: "academic_classes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "'Basic'::text");

            migrationBuilder.AddColumn<string>(
                name: "target_pathology_categories",
                table: "academic_classes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_student_level",
                table: "academic_classes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teaching_objectives",
                table: "academic_classes",
                type: "jsonb",
                nullable: true);

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
                    is_bookmarked = table.Column<bool>(type: "boolean", nullable: false),
                    bookmarked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_bone_specialty_id",
                table: "quizzes",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_created_by_lecturer_id",
                table: "quizzes",
                column: "created_by_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_pathology_category_id",
                table: "quizzes",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "idx_qa_messages_session_id",
                table: "qa_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_bone_specialty_id",
                table: "medical_cases",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_cases_pathology_category_id",
                table: "medical_cases",
                column: "pathology_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_case_view_logs_class_id",
                table: "case_view_logs",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "idx_case_metadata_bone_specialty_id",
                table: "case_metadata",
                column: "bone_specialty_id");

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
                name: "IX_error_patterns_student_id",
                table: "error_patterns",
                column: "student_id");

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
                name: "idx_pathology_categories_bone",
                table: "pathology_categories",
                column: "bone_specialty_id");

            migrationBuilder.CreateIndex(
                name: "idx_pathology_categories_code",
                table: "pathology_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quiz_review_items_attempt_id",
                table: "quiz_review_items",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_quiz_review_items_question_id",
                table: "quiz_review_items",
                column: "question_id");

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

            migrationBuilder.AddForeignKey(
                name: "bone_specialties_parent_id_fkey",
                table: "bone_specialties",
                column: "parent_id",
                principalTable: "bone_specialties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "case_view_logs_class_id_fkey",
                table: "case_view_logs",
                column: "class_id",
                principalTable: "academic_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "medical_cases_bone_specialty_fkey",
                table: "medical_cases",
                column: "bone_specialty_id",
                principalTable: "bone_specialties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "medical_cases_pathology_fkey",
                table: "medical_cases",
                column: "pathology_category_id",
                principalTable: "pathology_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_quizzes_users_created_by_lecturer_id",
                table: "quizzes",
                column: "created_by_lecturer_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "quizzes_bone_specialty_fkey",
                table: "quizzes",
                column: "bone_specialty_id",
                principalTable: "bone_specialties",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "quizzes_pathology_fkey",
                table: "quizzes",
                column: "pathology_category_id",
                principalTable: "pathology_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "student_questions_case_id_fkey",
                table: "student_questions",
                column: "case_id",
                principalTable: "medical_cases",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "bone_specialties_parent_id_fkey",
                table: "bone_specialties");

            migrationBuilder.DropForeignKey(
                name: "case_view_logs_class_id_fkey",
                table: "case_view_logs");

            migrationBuilder.DropForeignKey(
                name: "medical_cases_bone_specialty_fkey",
                table: "medical_cases");

            migrationBuilder.DropForeignKey(
                name: "medical_cases_pathology_fkey",
                table: "medical_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_quizzes_users_created_by_lecturer_id",
                table: "quizzes");

            migrationBuilder.DropForeignKey(
                name: "quizzes_bone_specialty_fkey",
                table: "quizzes");

            migrationBuilder.DropForeignKey(
                name: "quizzes_pathology_fkey",
                table: "quizzes");

            migrationBuilder.DropForeignKey(
                name: "student_questions_case_id_fkey",
                table: "student_questions");

            migrationBuilder.DropTable(
                name: "backups");

            migrationBuilder.DropTable(
                name: "competency_definitions");

            migrationBuilder.DropTable(
                name: "data_exports");

            migrationBuilder.DropTable(
                name: "error_patterns");

            migrationBuilder.DropTable(
                name: "flashcards");

            migrationBuilder.DropTable(
                name: "learning_insights");

            migrationBuilder.DropTable(
                name: "question_trends");

            migrationBuilder.DropTable(
                name: "quiz_review_items");

            migrationBuilder.DropTable(
                name: "review_schedules");

            migrationBuilder.DropTable(
                name: "student_competencies");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "flashcard_decks");

            migrationBuilder.DropTable(
                name: "pathology_categories");

            migrationBuilder.DropIndex(
                name: "IX_quizzes_bone_specialty_id",
                table: "quizzes");

            migrationBuilder.DropIndex(
                name: "IX_quizzes_created_by_lecturer_id",
                table: "quizzes");

            migrationBuilder.DropIndex(
                name: "IX_quizzes_pathology_category_id",
                table: "quizzes");

            migrationBuilder.DropIndex(
                name: "idx_qa_messages_session_id",
                table: "qa_messages");

            migrationBuilder.DropIndex(
                name: "IX_medical_cases_bone_specialty_id",
                table: "medical_cases");

            migrationBuilder.DropIndex(
                name: "IX_medical_cases_pathology_category_id",
                table: "medical_cases");

            migrationBuilder.DropIndex(
                name: "IX_case_view_logs_class_id",
                table: "case_view_logs");

            migrationBuilder.DropIndex(
                name: "idx_case_metadata_bone_specialty_id",
                table: "case_metadata");

            migrationBuilder.DropIndex(
                name: "idx_bone_specialties_code",
                table: "bone_specialties");

            migrationBuilder.DropIndex(
                name: "idx_bone_specialties_parent",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "review_feedback",
                table: "visual_qa_sessions");

            migrationBuilder.DropColumn(
                name: "adaptive_difficulty",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "bone_specialty_id",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "created_by_lecturer_id",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "learning_objectives",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "pathology_category_id",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "quiz_mode",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "spaced_repetition_enabled",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "target_student_level",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "teaching_points",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "accepted_answers",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "correct_answers",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "explanation",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "hint",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "current_question_index",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "difficulty_level",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "ease_factor",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "next_review_date",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "review_interval",
                table: "quiz_attempts");

            migrationBuilder.DropColumn(
                name: "body_part",
                table: "medical_images");

            migrationBuilder.DropColumn(
                name: "clinical_notes",
                table: "medical_images");

            migrationBuilder.DropColumn(
                name: "contrast_used",
                table: "medical_images");

            migrationBuilder.DropColumn(
                name: "image_quality",
                table: "medical_images");

            migrationBuilder.DropColumn(
                name: "view_type",
                table: "medical_images");

            migrationBuilder.DropColumn(
                name: "body_region",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "bone_specialty_id",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "emergency_level",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "pathology_category_id",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "patient_age_group",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "medical_cases");

            migrationBuilder.DropColumn(
                name: "quiz_mode",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "release_answers_at",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "released_by_id",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "shuffle_options",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "class_id",
                table: "case_view_logs");

            migrationBuilder.DropColumn(
                name: "code",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "description",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "bone_specialties");

            migrationBuilder.DropColumn(
                name: "focus_level",
                table: "academic_classes");

            migrationBuilder.DropColumn(
                name: "target_pathology_categories",
                table: "academic_classes");

            migrationBuilder.DropColumn(
                name: "target_student_level",
                table: "academic_classes");

            migrationBuilder.DropColumn(
                name: "teaching_objectives",
                table: "academic_classes");

            migrationBuilder.RenameIndex(
                name: "idx_case_media_case_id",
                table: "case_media",
                newName: "IX_case_media_case_id");

            migrationBuilder.RenameIndex(
                name: "idx_academic_classes_class_specialty_id",
                table: "academic_classes",
                newName: "IX_academic_classes_class_specialty_id");

            migrationBuilder.AlterColumn<int>(
                name: "time_limit",
                table: "quizzes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "passing_score",
                table: "quizzes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "time_limit_minutes",
                table: "class_quiz_sessions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "passing_score",
                table: "class_quiz_sessions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "bone_specialties_name_key",
                table: "bone_specialties",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "student_questions_case_id_fkey",
                table: "student_questions",
                column: "case_id",
                principalTable: "medical_cases",
                principalColumn: "id");
        }
    }
}

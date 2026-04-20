using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalChunksToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "citations_answer_id_fkey",
                table: "citations");

            migrationBuilder.DropForeignKey(
                name: "expert_reviews_answer_id_fkey",
                table: "expert_reviews");

            migrationBuilder.DropIndex(
                name: "citations_answer_id_chunk_id_key",
                table: "citations");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bio",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "class_code",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emergency_contact",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medical_school",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "medical_student_id",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specialty",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "student_id",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_notes",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_status",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "classification",
                table: "quizzes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "difficulty",
                table: "quizzes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ai_generated",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_verified_curriculum",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                table: "quizzes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "quiz_questions",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "version",
                table: "medical_cases",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "indexing_status",
                table: "medical_cases",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<Guid>(
                name: "answer_id",
                table: "expert_reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                table: "expert_reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "indexing_progress",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_chunks",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "flag_reason",
                table: "document_chunks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "flagged_at",
                table: "document_chunks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "flagged_by_expert_id",
                table: "document_chunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_late",
                table: "class_quiz_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allow_retake",
                table: "class_quiz_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "retake_reset_at",
                table: "class_quiz_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_results_after_submission",
                table: "class_quiz_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "shuffle_questions",
                table: "class_quiz_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "answer_id",
                table: "citations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "message_id",
                table: "citations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key_imaging_findings",
                table: "case_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reflective_questions",
                table: "case_answers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "send_email",
                table: "announcements",
                type: "boolean",
                nullable: true,
                defaultValueSql: "true");

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
                name: "visual_qa_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_image_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lecturer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    promoted_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("visual_qa_sessions_pkey", x => x.id);
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
                name: "qa_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    coordinates = table.Column<string>(type: "jsonb", nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_users_verified_by",
                table: "users",
                column: "verified_by");

            migrationBuilder.CreateIndex(
                name: "expert_reviews_expert_id_session_id_key",
                table: "expert_reviews",
                columns: new[] { "expert_id", "session_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expert_reviews_session_id",
                table: "expert_reviews",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ux_documents_content_hash",
                table: "documents",
                column: "content_hash",
                unique: true,
                filter: "\"content_hash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_flagged_by_expert_id",
                table: "document_chunks",
                column: "flagged_by_expert_id");

            migrationBuilder.CreateIndex(
                name: "idx_citations_message",
                table: "citations",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_qa_messages_role",
                table: "qa_messages",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "idx_qa_messages_session_created_at",
                table: "qa_messages",
                columns: new[] { "session_id", "created_at" });

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

            migrationBuilder.AddForeignKey(
                name: "citations_answer_id_fkey",
                table: "citations",
                column: "answer_id",
                principalTable: "case_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "citations_message_id_fkey",
                table: "citations",
                column: "message_id",
                principalTable: "qa_messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "document_chunks_flagged_by_expert_id_fkey",
                table: "document_chunks",
                column: "flagged_by_expert_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "expert_reviews_answer_id_fkey",
                table: "expert_reviews",
                column: "answer_id",
                principalTable: "case_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "expert_reviews_session_id_fkey",
                table: "expert_reviews",
                column: "session_id",
                principalTable: "visual_qa_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "users_verified_by_fkey",
                table: "users",
                column: "verified_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "citations_answer_id_fkey",
                table: "citations");

            migrationBuilder.DropForeignKey(
                name: "citations_message_id_fkey",
                table: "citations");

            migrationBuilder.DropForeignKey(
                name: "document_chunks_flagged_by_expert_id_fkey",
                table: "document_chunks");

            migrationBuilder.DropForeignKey(
                name: "expert_reviews_answer_id_fkey",
                table: "expert_reviews");

            migrationBuilder.DropForeignKey(
                name: "expert_reviews_session_id_fkey",
                table: "expert_reviews");

            migrationBuilder.DropForeignKey(
                name: "users_verified_by_fkey",
                table: "users");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "qa_messages");

            migrationBuilder.DropTable(
                name: "visual_qa_sessions");

            migrationBuilder.DropIndex(
                name: "IX_users_verified_by",
                table: "users");

            migrationBuilder.DropIndex(
                name: "expert_reviews_expert_id_session_id_key",
                table: "expert_reviews");

            migrationBuilder.DropIndex(
                name: "IX_expert_reviews_session_id",
                table: "expert_reviews");

            migrationBuilder.DropIndex(
                name: "ux_documents_content_hash",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_document_chunks_flagged_by_expert_id",
                table: "document_chunks");

            migrationBuilder.DropIndex(
                name: "idx_citations_message",
                table: "citations");

            migrationBuilder.DropColumn(
                name: "address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "bio",
                table: "users");

            migrationBuilder.DropColumn(
                name: "class_code",
                table: "users");

            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "department",
                table: "users");

            migrationBuilder.DropColumn(
                name: "emergency_contact",
                table: "users");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "users");

            migrationBuilder.DropColumn(
                name: "medical_school",
                table: "users");

            migrationBuilder.DropColumn(
                name: "medical_student_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "specialty",
                table: "users");

            migrationBuilder.DropColumn(
                name: "student_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verification_notes",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verification_status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verified_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "classification",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "difficulty",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "is_ai_generated",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "is_verified_curriculum",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "topic",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "quiz_questions");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "expert_reviews");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "indexing_progress",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "total_chunks",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "flag_reason",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "flagged_at",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "flagged_by_expert_id",
                table: "document_chunks");

            migrationBuilder.DropColumn(
                name: "allow_late",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "allow_retake",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "retake_reset_at",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "show_results_after_submission",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "shuffle_questions",
                table: "class_quiz_sessions");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "citations");

            migrationBuilder.DropColumn(
                name: "key_imaging_findings",
                table: "case_answers");

            migrationBuilder.DropColumn(
                name: "reflective_questions",
                table: "case_answers");

            migrationBuilder.DropColumn(
                name: "send_email",
                table: "announcements");

            migrationBuilder.AlterColumn<int>(
                name: "version",
                table: "medical_cases",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "indexing_status",
                table: "medical_cases",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "answer_id",
                table: "expert_reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "answer_id",
                table: "citations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "citations_answer_id_chunk_id_key",
                table: "citations",
                columns: new[] { "answer_id", "chunk_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "citations_answer_id_fkey",
                table: "citations",
                column: "answer_id",
                principalTable: "case_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "expert_reviews_answer_id_fkey",
                table: "expert_reviews",
                column: "answer_id",
                principalTable: "case_answers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoneVisQA.Repositories.Migrations;

/// <inheritdoc />
public partial class VisualQaStudyModeAndMedicalCaseDeleteFk : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE visual_qa_sessions
            ADD COLUMN IF NOT EXISTS study_mode character varying(32) NOT NULL DEFAULT 'personal_dicom';

            CREATE INDEX IF NOT EXISTS idx_visual_qa_sessions_study_mode
            ON visual_qa_sessions (study_mode);

            UPDATE visual_qa_sessions vqs
            SET study_mode = 'catalog_case_study'
            FROM medical_cases mc
            WHERE vqs.case_id = mc.id
              AND mc.is_approved = true
              AND mc.owner_student_id IS NULL
              AND mc.created_by_expert_id IS NOT NULL
              AND COALESCE(vqs.study_mode, 'personal_dicom') = 'personal_dicom';

            ALTER TABLE visual_qa_sessions
            ALTER COLUMN case_id DROP NOT NULL;

            ALTER TABLE visual_qa_sessions DROP CONSTRAINT IF EXISTS vqs_case_fk;
            ALTER TABLE visual_qa_sessions DROP CONSTRAINT IF EXISTS visual_qa_sessions_case_id_fkey;

            ALTER TABLE visual_qa_sessions
            ADD CONSTRAINT visual_qa_sessions_case_id_fkey
            FOREIGN KEY (case_id) REFERENCES medical_cases (id) ON DELETE SET NULL;

            ALTER TABLE visual_qa_sessions DROP CONSTRAINT IF EXISTS FK_visual_qa_sessions_medical_cases_promoted_case_id;

            ALTER TABLE visual_qa_sessions
            ADD CONSTRAINT FK_visual_qa_sessions_medical_cases_promoted_case_id
            FOREIGN KEY (promoted_case_id) REFERENCES medical_cases (id) ON DELETE SET NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE visual_qa_sessions DROP CONSTRAINT IF EXISTS FK_visual_qa_sessions_medical_cases_promoted_case_id;
            ALTER TABLE visual_qa_sessions DROP CONSTRAINT IF EXISTS visual_qa_sessions_case_id_fkey;

            ALTER TABLE visual_qa_sessions
            ADD CONSTRAINT visual_qa_sessions_case_id_fkey
            FOREIGN KEY (case_id) REFERENCES medical_cases (id) ON DELETE SET NULL;

            ALTER TABLE visual_qa_sessions
            ADD CONSTRAINT FK_visual_qa_sessions_medical_cases_promoted_case_id
            FOREIGN KEY (promoted_case_id) REFERENCES medical_cases (id);

            DROP INDEX IF EXISTS idx_visual_qa_sessions_study_mode;
            ALTER TABLE visual_qa_sessions DROP COLUMN IF EXISTS study_mode;
            """);
    }
}

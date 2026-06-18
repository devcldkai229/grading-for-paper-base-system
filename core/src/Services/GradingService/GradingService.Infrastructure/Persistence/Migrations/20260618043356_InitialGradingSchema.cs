using System;
using GradingService.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGradingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:ai_review_status", "pending,accepted,rejected,modified")
                .Annotation("Npgsql:Enum:ai_sync_status", "not_requested,queued,processing,completed,failed")
                .Annotation("Npgsql:Enum:assignment_type", "first_grade,cross_grade,re_grade")
                .Annotation("Npgsql:Enum:grading_progress_status", "not_started,drafting,submitted");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grading_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_paper_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_type = table.Column<AssignmentType>(type: "assignment_type", nullable: false),
                    status = table.Column<GradingProgressStatus>(type: "grading_progress_status", nullable: false),
                    ai_status = table.Column<AiSyncStatus>(type: "ai_sync_status", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marker_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_start = table.Column<int>(type: "integer", nullable: false),
                    alias_end = table.Column<int>(type: "integer", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marker_assignments", x => x.id);
                    table.CheckConstraint("CK_marker_assignments_alias_end", "alias_end >= alias_start");
                    table.CheckConstraint("CK_marker_assignments_alias_start", "alias_start >= 1");
                });

            migrationBuilder.CreateTable(
                name: "grading_forms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    paper_comment = table.Column<string>(type: "text", nullable: true),
                    general_comment = table.Column<string>(type: "text", nullable: true),
                    internal_comment = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_forms", x => x.id);
                    table.ForeignKey(
                        name: "FK_grading_forms_grading_assignments_grading_assignment_id",
                        column: x => x.grading_assignment_id,
                        principalTable: "grading_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_grade_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grading_form_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    question_comment = table.Column<string>(type: "text", nullable: true),
                    ai_drafted = table.Column<bool>(type: "boolean", nullable: false),
                    ai_review_status = table.Column<AiReviewStatus>(type: "ai_review_status", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_grade_details", x => x.id);
                    table.CheckConstraint("CK_question_grade_details_score_range", "score >= 0 AND score <= max_score");
                    table.ForeignKey(
                        name: "FK_question_grade_details_grading_forms_grading_form_id",
                        column: x => x.grading_form_id,
                        principalTable: "grading_forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "idx_audit_user",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_assign_ai_status",
                table: "grading_assignments",
                column: "ai_status");

            migrationBuilder.CreateIndex(
                name: "idx_assign_paper",
                table: "grading_assignments",
                column: "student_paper_id");

            migrationBuilder.CreateIndex(
                name: "idx_assign_status",
                table: "grading_assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_assign_subject",
                table: "grading_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "idx_assign_teacher",
                table: "grading_assignments",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_grading_assignments_student_paper_id_teacher_id_assignment_~",
                table: "grading_assignments",
                columns: new[] { "student_paper_id", "teacher_id", "assignment_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grading_forms_grading_assignment_id",
                table: "grading_forms",
                column: "grading_assignment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_marker_assign_subject",
                table: "marker_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "idx_marker_assign_teacher",
                table: "marker_assignments",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_marker_assignments_subject_id_alias_start_alias_end",
                table: "marker_assignments",
                columns: new[] { "subject_id", "alias_start", "alias_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_grade_details_form",
                table: "question_grade_details",
                column: "grading_form_id");

            migrationBuilder.CreateIndex(
                name: "IX_question_grade_details_grading_form_id_question_number",
                table: "question_grade_details",
                columns: new[] { "grading_form_id", "question_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "marker_assignments");

            migrationBuilder.DropTable(
                name: "question_grade_details");

            migrationBuilder.DropTable(
                name: "grading_forms");

            migrationBuilder.DropTable(
                name: "grading_assignments");
        }
    }
}

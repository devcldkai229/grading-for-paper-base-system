using System;
using ExamCatalogService.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialExamCatalogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:exam_type", "FE,PE,PT,OTHER")
                .Annotation("Npgsql:Enum:subject_status", "draft,open,grading,closed")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.id);
                    table.CheckConstraint("chk_semester_dates", "end_date > start_date");
                });

            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    exam_type = table.Column<ExamType>(type: "exam_type", nullable: false, defaultValue: ExamType.FE),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.id);
                    table.ForeignKey(
                        name: "FK_exams_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 10m),
                    exam_paper_s3_key = table.Column<string>(type: "text", nullable: true),
                    exam_paper_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    exam_paper_content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rubric_s3_key = table.Column<string>(type: "text", nullable: true),
                    rubric_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rubric_content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    rubric_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<SubjectStatus>(type: "subject_status", nullable: false, defaultValue: SubjectStatus.Draft),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subjects", x => x.id);
                    table.CheckConstraint("CK_subjects_max_score", "max_score > 0");
                    table.ForeignKey(
                        name: "FK_subjects_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.id);
                    table.CheckConstraint("CK_questions_max_score", "max_score > 0");
                    table.ForeignKey(
                        name: "FK_questions_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_exams_semester",
                table: "exams",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "idx_questions_subject",
                table: "questions",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_questions_subject_id_question_number",
                table: "questions",
                columns: new[] { "subject_id", "question_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_semesters_code",
                table: "semesters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_subjects_exam",
                table: "subjects",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "idx_subjects_status",
                table: "subjects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_subjects_exam_id_subject_code",
                table: "subjects",
                columns: new[] { "exam_id", "subject_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "subjects");

            migrationBuilder.DropTable(
                name: "exams");

            migrationBuilder.DropTable(
                name: "semesters");
        }
    }
}

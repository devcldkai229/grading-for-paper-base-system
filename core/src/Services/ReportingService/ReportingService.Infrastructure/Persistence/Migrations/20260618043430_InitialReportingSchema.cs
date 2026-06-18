using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ReportingService.Domain.Enums;

#nullable disable

namespace ReportingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialReportingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:export_status", "queued,processing,completed,failed");

            migrationBuilder.CreateTable(
                name: "export_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    result_s3_key = table.Column<string>(type: "text", nullable: true),
                    result_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<ExportStatus>(type: "export_status", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marker_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_count = table.Column<int>(type: "integer", nullable: false),
                    completed_count = table.Column<int>(type: "integer", nullable: false),
                    drafting_count = table.Column<int>(type: "integer", nullable: false),
                    avg_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    throughput_per_hour = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marker_progress", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subject_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_papers = table.Column<int>(type: "integer", nullable: false),
                    completed_papers = table.Column<int>(type: "integer", nullable: false),
                    in_progress = table.Column<int>(type: "integer", nullable: false),
                    not_started = table.Column<int>(type: "integer", nullable: false),
                    score_avg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    score_min = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    score_max = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    estimated_finish = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject_progress", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_export_jobs_status",
                table: "export_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_export_jobs_subject",
                table: "export_jobs",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "idx_marker_progress_subj",
                table: "marker_progress",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "idx_marker_progress_tea",
                table: "marker_progress",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_marker_progress_subject_id_teacher_id",
                table: "marker_progress",
                columns: new[] { "subject_id", "teacher_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subject_progress_subject_id",
                table: "subject_progress",
                column: "subject_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "export_jobs");

            migrationBuilder.DropTable(
                name: "marker_progress");

            migrationBuilder.DropTable(
                name: "subject_progress");
        }
    }
}

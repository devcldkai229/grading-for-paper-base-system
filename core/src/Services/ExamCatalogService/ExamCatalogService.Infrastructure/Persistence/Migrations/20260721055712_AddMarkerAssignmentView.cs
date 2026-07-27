using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkerAssignmentView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marker_assignment_view",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_start = table.Column<int>(type: "integer", nullable: false),
                    alias_end = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marker_assignment_view", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marker_assignment_view_subject_id",
                table: "marker_assignment_view",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_marker_assignment_view_teacher_id",
                table: "marker_assignment_view",
                column: "teacher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marker_assignment_view");
        }
    }
}

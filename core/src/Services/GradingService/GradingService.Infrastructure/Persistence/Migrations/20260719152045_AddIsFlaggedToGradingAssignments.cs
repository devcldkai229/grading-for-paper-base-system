using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFlaggedToGradingAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_flagged",
                table: "grading_assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_assign_flagged",
                table: "grading_assignments",
                column: "is_flagged");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_assign_flagged",
                table: "grading_assignments");

            migrationBuilder.DropColumn(
                name: "is_flagged",
                table: "grading_assignments");
        }
    }
}

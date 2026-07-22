using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSuggestionsToGradingForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_paper_comment",
                table: "grading_forms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_suggestions_json",
                table: "grading_forms",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_paper_comment",
                table: "grading_forms");

            migrationBuilder.DropColumn(
                name: "ai_suggestions_json",
                table: "grading_forms");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionGradeDetailGridSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "group_label",
                table: "question_grade_details",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "question_grade_details",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "order_index",
                table: "question_grade_details",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "group_label",
                table: "question_grade_details");

            migrationBuilder.DropColumn(
                name: "label",
                table: "question_grade_details");

            migrationBuilder.DropColumn(
                name: "order_index",
                table: "question_grade_details");
        }
    }
}

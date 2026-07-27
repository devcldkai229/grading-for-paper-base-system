using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPassScoreToSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "pass_score",
                table: "subjects",
                type: "numeric(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pass_score",
                table: "subjects");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingDeadlineToSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "grading_deadline",
                table: "subjects",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grading_deadline",
                table: "subjects");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreGridTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "score_grid_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    questions_json = table.Column<string>(type: "jsonb", nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_grid_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_score_grid_templates_name",
                table: "score_grid_templates",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_grid_templates");
        }
    }
}

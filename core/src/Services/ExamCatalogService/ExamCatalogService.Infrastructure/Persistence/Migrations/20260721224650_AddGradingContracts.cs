using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grading_contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_version = table.Column<int>(type: "integer", nullable: false),
                    contract_json = table.Column<string>(type: "jsonb", nullable: false),
                    coverage_ok = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    model_used = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grading_contracts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rubric_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubric_version = table.Column<int>(type: "integer", nullable: false),
                    asset_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    question = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    page_index = table.Column<int>(type: "integer", nullable: false),
                    s3_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_assets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_grading_contracts_subject_version",
                table: "grading_contracts",
                columns: new[] { "subject_id", "rubric_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_rubric_assets_subject_version",
                table: "rubric_assets",
                columns: new[] { "subject_id", "rubric_version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grading_contracts");

            migrationBuilder.DropTable(
                name: "rubric_assets");
        }
    }
}

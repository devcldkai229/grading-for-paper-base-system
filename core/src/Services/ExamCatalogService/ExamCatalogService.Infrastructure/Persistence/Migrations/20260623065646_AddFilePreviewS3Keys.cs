using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamCatalogService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilePreviewS3Keys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exam_paper_preview_content_type",
                table: "subjects",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exam_paper_preview_s3_key",
                table: "subjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rubric_preview_content_type",
                table: "subjects",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rubric_preview_s3_key",
                table: "subjects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exam_paper_preview_content_type",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "exam_paper_preview_s3_key",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "rubric_preview_content_type",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "rubric_preview_s3_key",
                table: "subjects");
        }
    }
}

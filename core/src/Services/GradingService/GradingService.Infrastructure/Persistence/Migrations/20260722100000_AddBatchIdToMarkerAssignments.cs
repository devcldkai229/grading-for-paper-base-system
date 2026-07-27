using GradingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(GradingDbContext))]
    [Migration("20260722100000_AddBatchIdToMarkerAssignments")]
    public partial class AddBatchIdToMarkerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                table: "marker_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "zip_file_name",
                table: "marker_assignments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_marker_assignments_subject_id_batch_id",
                table: "marker_assignments",
                columns: new[] { "subject_id", "batch_id" },
                unique: true,
                filter: "batch_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_marker_assignments_subject_id_batch_id",
                table: "marker_assignments");

            migrationBuilder.DropColumn(
                name: "zip_file_name",
                table: "marker_assignments");

            migrationBuilder.DropColumn(
                name: "batch_id",
                table: "marker_assignments");
        }
    }
}

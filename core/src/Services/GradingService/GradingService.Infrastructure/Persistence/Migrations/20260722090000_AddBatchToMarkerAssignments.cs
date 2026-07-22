using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GradingDbContext))]
[Migration("20260722090000_AddBatchToMarkerAssignments")]
public sealed class AddBatchToMarkerAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_marker_assignments_subject_id_alias_start_alias_end",
            table: "marker_assignments");

        migrationBuilder.AddColumn<Guid>(
            name: "batch_id",
            table: "marker_assignments",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.CreateIndex(
            name: "idx_marker_assign_batch",
            table: "marker_assignments",
            column: "batch_id");

        migrationBuilder.CreateIndex(
            name: "IX_marker_assignments_batch_id_alias_start_alias_end",
            table: "marker_assignments",
            columns: new[] { "batch_id", "alias_start", "alias_end" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "idx_marker_assign_batch", table: "marker_assignments");
        migrationBuilder.DropIndex(
            name: "IX_marker_assignments_batch_id_alias_start_alias_end",
            table: "marker_assignments");
        migrationBuilder.DropColumn(name: "batch_id", table: "marker_assignments");
        migrationBuilder.CreateIndex(
            name: "IX_marker_assignments_subject_id_alias_start_alias_end",
            table: "marker_assignments",
            columns: new[] { "subject_id", "alias_start", "alias_end" },
            unique: true);
    }
}

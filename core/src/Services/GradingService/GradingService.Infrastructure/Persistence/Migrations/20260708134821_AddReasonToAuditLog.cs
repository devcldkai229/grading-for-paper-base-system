using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GradingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReasonToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "audit_logs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reason",
                table: "audit_logs");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationUserIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notifications_unread",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_unread",
                table: "notifications",
                columns: new[] { "user_id", "is_read" },
                filter: "is_read = FALSE");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_user",
                table: "notifications",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notifications_unread",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "idx_notifications_user",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_unread",
                table: "notifications",
                column: "user_id",
                filter: "is_read = FALSE");
        }
    }
}

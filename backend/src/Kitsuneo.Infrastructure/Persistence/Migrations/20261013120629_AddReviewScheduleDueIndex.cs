using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitsuneo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewScheduleDueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_review_schedules_DueAt",
                table: "review_schedules",
                column: "DueAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_review_schedules_DueAt",
                table: "review_schedules");
        }
    }
}

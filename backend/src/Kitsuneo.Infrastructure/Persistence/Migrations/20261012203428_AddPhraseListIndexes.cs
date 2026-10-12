using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitsuneo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhraseListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phrases_UserId_Language",
                table: "phrases");

            migrationBuilder.CreateIndex(
                name: "IX_phrases_UserId_CreatedAt_Id",
                table: "phrases",
                columns: new[] { "UserId", "CreatedAt", "Id" },
                filter: "\"IsArchived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_phrases_UserId_Language_CreatedAt_Id",
                table: "phrases",
                columns: new[] { "UserId", "Language", "CreatedAt", "Id" },
                filter: "\"IsArchived\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phrases_UserId_CreatedAt_Id",
                table: "phrases");

            migrationBuilder.DropIndex(
                name: "IX_phrases_UserId_Language_CreatedAt_Id",
                table: "phrases");

            migrationBuilder.CreateIndex(
                name: "IX_phrases_UserId_Language",
                table: "phrases",
                columns: new[] { "UserId", "Language" });
        }
    }
}

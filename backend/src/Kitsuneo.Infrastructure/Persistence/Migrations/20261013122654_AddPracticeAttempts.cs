using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitsuneo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practice_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    UsedSlowSpeed = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueAtBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueAtAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practice_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_practice_attempts_phrases_PhraseId",
                        column: x => x.PhraseId,
                        principalTable: "phrases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_practice_attempts_PhraseId_CreatedAt",
                table: "practice_attempts",
                columns: new[] { "PhraseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_practice_attempts_UserId_CreatedAt",
                table: "practice_attempts",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practice_attempts");
        }
    }
}

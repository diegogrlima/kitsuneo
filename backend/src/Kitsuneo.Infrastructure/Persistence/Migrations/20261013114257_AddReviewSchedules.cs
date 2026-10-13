using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitsuneo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_schedules",
                columns: table => new
                {
                    PhraseId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: true),
                    Stability = table.Column<double>(type: "double precision", nullable: true),
                    Difficulty = table.Column<double>(type: "double precision", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_schedules", x => x.PhraseId);
                    table.ForeignKey(
                        name: "FK_review_schedules_phrases_PhraseId",
                        column: x => x.PhraseId,
                        principalTable: "phrases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Phrases created before this migration get a new schedule, due since they were created
            migrationBuilder.Sql("""
                INSERT INTO review_schedules ("PhraseId", "State", "Step", "DueAt")
                SELECT "Id", 'Learning', 0, "CreatedAt" FROM phrases;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_schedules");
        }
    }
}

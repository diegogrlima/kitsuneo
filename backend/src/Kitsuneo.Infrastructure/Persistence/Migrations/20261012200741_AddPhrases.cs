using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kitsuneo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhrases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "phrases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SpokenForm = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reading = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Translation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_phrases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_phrases_UserId_Language",
                table: "phrases",
                columns: new[] { "UserId", "Language" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "phrases");
        }
    }
}

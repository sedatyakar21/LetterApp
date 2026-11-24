using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiplePostcards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "LetterPostcards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostcardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LetterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterPostcards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterPostcards_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LetterPostcards_LetterId",
                table: "LetterPostcards",
                column: "LetterId");

            migrationBuilder.CreateIndex(
                name: "IX_LetterPostcards_PostcardId",
                table: "LetterPostcards",
                column: "PostcardId");

            // Mektuptaki tek kartpostal (kolonlar) yeni tabloya taşınır, sonra kolonlar silinir.
            migrationBuilder.Sql("""
                INSERT INTO LetterPostcards (LetterId, PostcardId, Title, Category)
                SELECT Id, PostcardId, PostcardTitle, PostcardCategory FROM Letters
                WHERE PostcardTitle IS NOT NULL AND PostcardCategory IS NOT NULL;
                """);
            migrationBuilder.DropColumn(
                name: "PostcardCategory",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "PostcardId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "PostcardTitle",
                table: "Letters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LetterPostcards");

            migrationBuilder.AddColumn<string>(
                name: "PostcardCategory",
                table: "Letters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostcardId",
                table: "Letters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostcardTitle",
                table: "Letters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}

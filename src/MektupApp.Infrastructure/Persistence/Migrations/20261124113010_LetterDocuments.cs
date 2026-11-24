using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LetterDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Document",
                table: "PricingSettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                // Mevcut fiyat kaydı belge ücretini 0 değil varsayılan 25 ₺ olarak alsın (panelden değiştirilebilir).
                defaultValue: 25m);

            migrationBuilder.CreateTable(
                name: "LetterDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterDocuments_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LetterDocuments_LetterId",
                table: "LetterDocuments",
                column: "LetterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LetterDocuments");

            migrationBuilder.DropColumn(
                name: "Document",
                table: "PricingSettings");
        }
    }
}

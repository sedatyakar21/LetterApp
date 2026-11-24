using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostcardCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PostcardCategory",
                table: "Letters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

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

            // Eski hazır tasarımlı kartpostallar (kategori enum adı + tasarım no) okunur bilgiye çevrilir:
            // "Love" + 2 → kategori "Aşk", ad "Aşk #2". Katalogda karşılığı olmadığı için PostcardId boş kalır.
            migrationBuilder.Sql("""
                UPDATE Letters SET PostcardCategory = CASE PostcardCategory
                        WHEN 'Love' THEN N'Aşk'
                        WHEN 'Nature' THEN N'Doğa'
                        WHEN 'Cities' THEN N'Şehirler'
                        WHEN 'Celebration' THEN N'Özel Gün'
                        WHEN 'Motivation' THEN N'Motivasyon'
                        ELSE PostcardCategory END
                WHERE PostcardCategory IS NOT NULL;
                UPDATE Letters SET PostcardTitle = PostcardCategory + N' #' + CAST(ISNULL(PostcardDesign, 1) AS nvarchar(10))
                WHERE PostcardCategory IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "PostcardDesign",
                table: "Letters");

            migrationBuilder.CreateTable(
                name: "Postcards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postcards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Postcards_IsActive_SortOrder",
                table: "Postcards",
                columns: new[] { "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Postcards");

            migrationBuilder.DropColumn(
                name: "PostcardId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "PostcardTitle",
                table: "Letters");

            migrationBuilder.AlterColumn<string>(
                name: "PostcardCategory",
                table: "Letters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostcardDesign",
                table: "Letters",
                type: "int",
                nullable: true);
        }
    }
}

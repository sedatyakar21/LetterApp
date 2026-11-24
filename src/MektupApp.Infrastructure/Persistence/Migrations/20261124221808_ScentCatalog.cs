using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScentId",
                table: "Letters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScentName",
                table: "Letters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Scents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Scents_IsActive_SortOrder",
                table: "Scents",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Scents_Name",
                table: "Scents",
                column: "Name",
                unique: true);

            // Önceden koda gömülü üç koku listeye eklenir (sabit kimliklerle); eski siparişler bunlara bağlanır.
            migrationBuilder.Sql("""
                DECLARE @now datetime2 = SYSUTCDATETIME();
                INSERT INTO Scents (Id, Name, IsActive, SortOrder, CreatedAt, UpdatedAt) VALUES
                    ('5ce47000-0000-0000-0000-000000000001', N'Manolya', 1, 1, @now, @now),
                    ('5ce47000-0000-0000-0000-000000000002', N'Lavanta', 1, 2, @now, @now),
                    ('5ce47000-0000-0000-0000-000000000003', N'Bahar Çiçekleri', 1, 3, @now, @now);
                UPDATE Letters SET
                    ScentId = CASE Scent
                        WHEN 'Magnolia' THEN '5ce47000-0000-0000-0000-000000000001'
                        WHEN 'Lavender' THEN '5ce47000-0000-0000-0000-000000000002'
                        WHEN 'SpringFlowers' THEN '5ce47000-0000-0000-0000-000000000003' END,
                    ScentName = CASE Scent
                        WHEN 'Magnolia' THEN N'Manolya'
                        WHEN 'Lavender' THEN N'Lavanta'
                        WHEN 'SpringFlowers' THEN N'Bahar Çiçekleri' END
                WHERE Scent IN ('Magnolia', 'Lavender', 'SpringFlowers');
                """);

            migrationBuilder.DropColumn(
                name: "Scent",
                table: "Letters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scents");

            migrationBuilder.DropColumn(
                name: "ScentId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "ScentName",
                table: "Letters");

            migrationBuilder.AddColumn<string>(
                name: "Scent",
                table: "Letters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}

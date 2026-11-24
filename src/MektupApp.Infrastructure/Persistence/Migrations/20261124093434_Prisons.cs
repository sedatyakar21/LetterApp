using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Prisons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prisons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    District = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prisons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prisons_City_IsActive_Name",
                table: "Prisons",
                columns: new[] { "City", "IsActive", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prisons");
        }
    }
}

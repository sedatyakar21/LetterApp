using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PttCarrier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Carrier",
                table: "Letters",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierCheckedAt",
                table: "Letters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierLastMovement",
                table: "Letters",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CarrierLastMovementAt",
                table: "Letters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierReference",
                table: "Letters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarrierBarcodeCounters",
                columns: table => new
                {
                    Carrier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextSerial = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierBarcodeCounters", x => x.Carrier);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Letters_Status_Carrier_CarrierCheckedAt",
                table: "Letters",
                columns: new[] { "Status", "Carrier", "CarrierCheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarrierBarcodeCounters");

            migrationBuilder.DropIndex(
                name: "IX_Letters_Status_Carrier_CarrierCheckedAt",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "Carrier",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CarrierCheckedAt",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CarrierLastMovement",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CarrierLastMovementAt",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CarrierReference",
                table: "Letters");
        }
    }
}

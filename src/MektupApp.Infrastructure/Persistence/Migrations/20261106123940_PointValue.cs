using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PointValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PointValue",
                table: "LoyaltySettings",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 1m); // şimdiye kadarki kural: 1 puan = 1 ₺ (0 olsaydı bölmede hata verirdi)

            migrationBuilder.AddColumn<decimal>(
                name: "PointsDiscount",
                table: "Letters",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Eski ödemelerde 1 puan = 1 ₺ idi: indirim tutarı kullanılan puana eşittir.
            migrationBuilder.Sql("UPDATE Letters SET PointsDiscount = PointsUsed WHERE PointsUsed > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PointValue",
                table: "LoyaltySettings");

            migrationBuilder.DropColumn(
                name: "PointsDiscount",
                table: "Letters");
        }
    }
}

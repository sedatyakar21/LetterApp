using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CampaignDiscount",
                table: "Payments",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignTitle",
                table: "Payments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CampaignDiscount",
                table: "Letters",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Letters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignTitle",
                table: "Letters",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Campaigns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None"); // mevcut kampanyalar "sadece duyuru" olarak kalır

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Campaigns",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinOrderAmount",
                table: "Campaigns",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignDiscount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CampaignTitle",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CampaignDiscount",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "CampaignTitle",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "MinOrderAmount",
                table: "Campaigns");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignAudience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Campaigns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Everyone"); // mevcut kampanyalar herkese açık kalır

            migrationBuilder.AddColumn<int>(
                name: "AudienceDays",
                table: "Campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageLimitPerCustomer",
                table: "Campaigns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Letters_CustomerId_CampaignId",
                table: "Letters",
                columns: new[] { "CustomerId", "CampaignId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Letters_CustomerId_CampaignId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "AudienceDays",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "UsageLimitPerCustomer",
                table: "Campaigns");
        }
    }
}

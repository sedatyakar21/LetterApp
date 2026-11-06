using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Referrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReferralEnabled",
                table: "LoyaltySettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReferralRewardType",
                table: "LoyaltySettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Percentage"); // boş metin enum olarak okunamaz

            migrationBuilder.AddColumn<decimal>(
                name: "ReferralValue",
                table: "LoyaltySettings",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 3m); // LoyaltySettings.CreateDefault ile aynı (kapalı, %3)

            migrationBuilder.AddColumn<decimal>(
                name: "ReferralPointsToEarn",
                table: "Letters",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferrerCustomerId",
                table: "Letters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcquisitionChannel",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Customers",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByCustomerId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Letters_ReferrerCustomerId",
                table: "Letters",
                column: "ReferrerCustomerId");

            // Mevcut üyelere, uygulamadaki ReferralCode.Generate ile aynı biçimde (MK + 6 karakter,
            // 0/O ve 1/I hariç) referans no üret. Benzersiz index bundan sonra oluşturulur.
            migrationBuilder.Sql(@"
DECLARE @a varchar(32) = '23456789ABCDEFGHJKLMNPQRSTUVWXYZ';
UPDATE Customers SET ReferralCode = 'MK'
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
    + SUBSTRING(@a, ABS(CHECKSUM(NEWID())) % 32 + 1, 1)
WHERE ReferralCode = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReferralCode",
                table: "Customers",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReferredByCustomerId",
                table: "Customers",
                column: "ReferredByCustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Customers_ReferredByCustomerId",
                table: "Customers",
                column: "ReferredByCustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Customers_ReferredByCustomerId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Letters_ReferrerCustomerId",
                table: "Letters");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ReferralCode",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_ReferredByCustomerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ReferralEnabled",
                table: "LoyaltySettings");

            migrationBuilder.DropColumn(
                name: "ReferralRewardType",
                table: "LoyaltySettings");

            migrationBuilder.DropColumn(
                name: "ReferralValue",
                table: "LoyaltySettings");

            migrationBuilder.DropColumn(
                name: "ReferralPointsToEarn",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "ReferrerCustomerId",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "AcquisitionChannel",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ReferredByCustomerId",
                table: "Customers");
        }
    }
}

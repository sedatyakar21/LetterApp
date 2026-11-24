using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecipientFatherNameWard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecipientFatherName",
                table: "Letters",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientWard",
                table: "Letters",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecipientFatherName",
                table: "Letters");

            migrationBuilder.DropColumn(
                name: "RecipientWard",
                table: "Letters");
        }
    }
}

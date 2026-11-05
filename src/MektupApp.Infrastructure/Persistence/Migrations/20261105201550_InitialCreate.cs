using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Letters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 25000, nullable: false),
                    Scent = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaidAmount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackingCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostcardCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PostcardDesign = table.Column<int>(type: "int", nullable: true),
                    RecipientDetail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientCity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RecipientDistrict = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RecipientAddressLine = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RecipientPostalCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    SenderCity = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SenderDistrict = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SenderAddressLine = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SenderPostalCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    EnvelopeColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Font = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InkColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaperColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LetterPhotos",
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
                    table.PrimaryKey("PK_LetterPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterPhotos_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LetterStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LetterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LetterStatusHistory_Letters_LetterId",
                        column: x => x.LetterId,
                        principalTable: "Letters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetterPhotos_LetterId",
                table: "LetterPhotos",
                column: "LetterId");

            migrationBuilder.CreateIndex(
                name: "IX_Letters_CustomerId_IsDeleted",
                table: "Letters",
                columns: new[] { "CustomerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Letters_TrackingCode",
                table: "Letters",
                column: "TrackingCode");

            migrationBuilder.CreateIndex(
                name: "IX_LetterStatusHistory_LetterId",
                table: "LetterStatusHistory",
                column: "LetterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "LetterPhotos");

            migrationBuilder.DropTable(
                name: "LetterStatusHistory");

            migrationBuilder.DropTable(
                name: "Letters");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MektupApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Veri migration'ı: mevcut referans no'ları akılda kalıcı yeni formata çevirir
    /// (ad-soyad baş harfleri + 6 haneli sayı, ör. AY482915). Şema değişmez.
    /// Kural ReferralCode.Initials ile aynıdır: ilk ve son kelimenin baş harfi, tek kelimede ilk iki harf;
    /// Türkçe harfler sadeleştirilir (Ç→C, Ğ→G, İ/ı→I, Ö→O, Ş→S, Ü→U).
    /// </summary>
    public partial class ReferralCodeFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE c SET ReferralCode =
    UPPER(TRANSLATE(
        LEFT(n.Name, 1) +
        CASE WHEN CHARINDEX(' ', n.Name) > 0
             THEN LEFT(RIGHT(n.Name, CHARINDEX(' ', REVERSE(n.Name)) - 1), 1)
             ELSE COALESCE(NULLIF(SUBSTRING(n.Name, 2, 1), ''), 'X')
        END,
        N'çÇğĞıIiİöÖşŞüÜ', N'CCGGIIIIOOSSUU'))
    + CAST(100000 + ABS(CHECKSUM(NEWID())) % 900000 AS varchar(6))
FROM Customers c
CROSS APPLY (SELECT LTRIM(RTRIM(c.FullName)) AS Name) n;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski rastgele kodlara geri dönülmez; yeni kodlar geçerli kalır.
        }
    }
}

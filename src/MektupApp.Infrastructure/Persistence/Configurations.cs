using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class LetterConfiguration : IEntityTypeConfiguration<Letter>
{
    public void Configure(EntityTypeBuilder<Letter> b)
    {
        b.ToTable("Letters");
        b.HasKey(l => l.Id);
        b.Ignore(l => l.DomainEvents);

        b.Property(l => l.SenderName).HasMaxLength(100).IsRequired();
        b.Property(l => l.Content).HasMaxLength(Letter.MaxContentLength);
        b.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.Scent).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(l => l.PaidAmount).HasPrecision(10, 2);
        b.Property(l => l.TrackingCode).HasMaxLength(13);
        b.HasIndex(l => l.TrackingCode);
        b.HasIndex(l => l.PaidAt);
        b.Property(l => l.PointsUsed).HasPrecision(10, 2);
        b.Property(l => l.PointsDiscount).HasPrecision(10, 2);
        b.Property(l => l.CampaignDiscount).HasPrecision(10, 2);
        b.Property(l => l.CampaignTitle).HasMaxLength(120);
        b.HasIndex(l => new { l.CustomerId, l.CampaignId });

        b.Property(l => l.Carrier).HasMaxLength(20);
        b.Property(l => l.CarrierReference).HasMaxLength(100);
        b.Property(l => l.CarrierLastMovement).HasMaxLength(250);
        b.HasIndex(l => new { l.Status, l.Carrier, l.CarrierCheckedAt });
        b.Property(l => l.PointsToEarn).HasPrecision(10, 2);
        b.Property(l => l.ReferralPointsToEarn).HasPrecision(10, 2);
        b.HasIndex(l => l.ReferrerCustomerId);
        b.HasIndex(l => new { l.Status, l.UpdatedAt });
        b.HasIndex(l => new { l.CustomerId, l.IsDeleted });

        // Value object'ler ayrı tablo değil, Letters tablosunda kolon olarak saklanır.
        b.ComplexProperty(l => l.SenderAddress, a => a.MapAddress("Sender"));
        b.ComplexProperty(l => l.Recipient, r =>
        {
            r.Property(x => x.FullName).HasColumnName("RecipientName").HasMaxLength(100);
            r.Property(x => x.Type).HasColumnName("RecipientType").HasConversion<string>().HasMaxLength(20);
            r.Property(x => x.Detail).HasColumnName("RecipientDetail").HasMaxLength(200);
            r.ComplexProperty(x => x.Address, a => a.MapAddress("Recipient"));
        });
        b.ComplexProperty(l => l.Style, s =>
        {
            s.Property(x => x.Paper).HasColumnName("PaperColor").HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Envelope).HasColumnName("EnvelopeColor").HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Font).HasColumnName("Font").HasConversion<string>().HasMaxLength(20);
            s.Property(x => x.Ink).HasColumnName("InkColor").HasConversion<string>().HasMaxLength(20);
        });
        b.ComplexProperty(l => l.Postcard, p =>
        {
            p.Property(x => x.Category).HasColumnName("PostcardCategory").HasConversion<string>().HasMaxLength(20);
            p.Property(x => x.Design).HasColumnName("PostcardDesign");
        });

        b.HasMany(l => l.Photos).WithOne().HasForeignKey(p => p.LetterId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(l => l.Photos).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        b.HasMany(l => l.History).WithOne().HasForeignKey(h => h.LetterId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(l => l.History).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class LetterPhotoConfiguration : IEntityTypeConfiguration<LetterPhoto>
{
    public void Configure(EntityTypeBuilder<LetterPhoto> b)
    {
        b.ToTable("LetterPhotos");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.StorageKey).HasMaxLength(300).IsRequired();
        b.Property(p => p.OriginalFileName).HasMaxLength(255).IsRequired();
    }
}

internal sealed class LetterStatusChangeConfiguration : IEntityTypeConfiguration<LetterStatusChange>
{
    public void Configure(EntityTypeBuilder<LetterStatusChange> b)
    {
        b.ToTable("LetterStatusHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(h => h.Note).HasMaxLength(300);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(c => c.Id);
        b.Ignore(c => c.DomainEvents);
        b.Property(c => c.FullName).HasMaxLength(100).IsRequired();
        b.Property(c => c.Email).HasConversion(e => e.Value, v => new Email(v)).HasMaxLength(200).IsRequired();
        b.HasIndex(c => c.Email).IsUnique();
        b.Property(c => c.Phone).HasConversion(p => p.Value, v => new PhoneNumber(v)).HasMaxLength(10).IsRequired();
        b.Property(c => c.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(c => c.IsActive);
        b.HasIndex(c => c.CreatedAt);

        b.Property(c => c.AcquisitionChannel).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.ReferralCode).HasMaxLength(ReferralCode.Length).IsRequired();
        b.HasIndex(c => c.ReferralCode).IsUnique();
        b.HasIndex(c => c.ReferredByCustomerId);
        // Davet eden üyeye referans (silinirse bağ korunmaz, kayıt kalır).
        b.HasOne<Customer>().WithMany().HasForeignKey(c => c.ReferredByCustomerId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class PricingSettingsConfiguration : IEntityTypeConfiguration<PricingSettings>
{
    public void Configure(EntityTypeBuilder<PricingSettings> b)
    {
        b.ToTable("PricingSettings");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Ignore(s => s.DomainEvents);
        b.Property(s => s.UpdatedBy).HasMaxLength(200);
        b.ComplexProperty(s => s.Prices, p =>
        {
            p.Property(x => x.BasePrice).HasColumnName("BasePrice").HasPrecision(10, 2);
            p.Property(x => x.ExtraPage).HasColumnName("ExtraPage").HasPrecision(10, 2);
            p.Property(x => x.Photo).HasColumnName("Photo").HasPrecision(10, 2);
            p.Property(x => x.Postcard).HasColumnName("Postcard").HasPrecision(10, 2);
            p.Property(x => x.ColoredPaper).HasColumnName("ColoredPaper").HasPrecision(10, 2);
            p.Property(x => x.ColoredEnvelope).HasColumnName("ColoredEnvelope").HasPrecision(10, 2);
            p.Property(x => x.Scent).HasColumnName("Scent").HasPrecision(10, 2);
            p.Property(x => x.MobilePaymentSurchargeRate).HasColumnName("MobilePaymentSurchargeRate").HasPrecision(5, 4);
        });
    }
}

internal static class AddressMapping
{
    public static void MapAddress(this ComplexPropertyBuilder<PostalAddress> a, string prefix)
    {
        a.Property(x => x.Line).HasColumnName($"{prefix}AddressLine").HasMaxLength(250);
        a.Property(x => x.District).HasColumnName($"{prefix}District").HasMaxLength(60);
        a.Property(x => x.City).HasColumnName($"{prefix}City").HasMaxLength(60);
        a.Property(x => x.PostalCode).HasColumnName($"{prefix}PostalCode").HasMaxLength(5);
    }
}

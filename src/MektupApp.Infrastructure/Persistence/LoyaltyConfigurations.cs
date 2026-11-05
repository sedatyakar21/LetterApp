using MektupApp.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MektupApp.Infrastructure.Persistence;

internal sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> b)
    {
        b.ToTable("LoyaltyAccounts");
        b.HasKey(a => a.Id);
        b.Ignore(a => a.DomainEvents);
        b.HasIndex(a => a.CustomerId).IsUnique();
        b.Property(a => a.Balance).HasPrecision(12, 2);
        b.Property(a => a.TotalEarned).HasPrecision(12, 2);

        // İyimser eşzamanlılık: aynı bakiye iki ayrı istekte aynı anda harcanamasın.
        b.Property<byte[]>("RowVersion").IsRowVersion();

        b.HasMany(a => a.Transactions).WithOne().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(a => a.Transactions).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> b)
    {
        b.ToTable("PointTransactions");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.Points).HasPrecision(12, 2);
        b.Property(t => t.Note).HasMaxLength(300);
        b.HasIndex(t => t.LetterId);
    }
}

internal sealed class LoyaltySettingsConfiguration : IEntityTypeConfiguration<LoyaltySettings>
{
    public void Configure(EntityTypeBuilder<LoyaltySettings> b)
    {
        b.ToTable("LoyaltySettings");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Ignore(s => s.DomainEvents);
        b.Property(s => s.RewardType).HasConversion<string>().HasMaxLength(20);
        b.Property(s => s.Value).HasPrecision(10, 2);
        b.Property(s => s.UpdatedBy).HasMaxLength(200);
    }
}

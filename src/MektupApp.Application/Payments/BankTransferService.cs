using MektupApp.Application.Abstractions;
using MektupApp.Domain.Payments;

namespace MektupApp.Application.Payments;

public sealed record BankTransferSettingsDto(bool Enabled, decimal DiscountRate, string BankName, string AccountHolder,
    string Iban, string IbanDisplay, string? Note, int ValidDays, bool IsAvailable, DateTime UpdatedAt, string? UpdatedBy);

public sealed record UpdateBankTransferCommand(bool Enabled, decimal DiscountRate, string? BankName, string? AccountHolder,
    string? Iban, string? Note, int ValidDays);

/// <summary>Onay bekleyen havale / EFT siparişi (yönetim paneli listesi).</summary>
public sealed record PendingBankTransferDto(Guid LetterId, string OrderNumber, decimal Amount, decimal Discount,
    DateTime CreatedAt, DateTime DueAt);

/// <summary>Havale / EFT ayarları (yönetim paneli): açık / kapalı, indirim oranı, banka hesabı ve bekleme süresi.</summary>
public sealed class BankTransferService(IBankTransferSettingsRepository settings, IPaymentRepository payments, IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PendingBankTransferDto>> ListPendingAsync(CancellationToken ct = default)
    {
        var s = await LoadAsync(ct);
        return (await payments.ListPendingBankTransfersAsync(ct))
            .Select(p => new PendingBankTransferDto(p.LetterId, MektupApp.Domain.Letters.OrderNumber.Format(p.LetterId),
                p.Amount, p.BankTransferDiscount, p.CreatedAt, p.CreatedAt.AddDays(s.ValidDays)))
            .ToList();
    }

    public async Task<BankTransferSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await LoadAsync(ct);
        return new BankTransferSettingsDto(s.Enabled, s.DiscountRate, s.BankName, s.AccountHolder, s.Iban, s.IbanDisplay,
            s.Note, s.ValidDays, s.IsAvailable, s.UpdatedAt, s.UpdatedBy);
    }

    public async Task UpdateAsync(UpdateBankTransferCommand cmd, string updatedBy, CancellationToken ct = default)
    {
        (await LoadAsync(ct)).Update(cmd.Enabled, cmd.DiscountRate, cmd.BankName, cmd.AccountHolder, cmd.Iban, cmd.Note,
            cmd.ValidDays, updatedBy);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<BankTransferSettings> LoadAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        if (s is not null) return s;
        s = BankTransferSettings.CreateDefault();
        settings.Add(s);
        return s;
    }
}

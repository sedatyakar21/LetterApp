using MektupApp.Application.Abstractions;
using MektupApp.Application.Loyalty;
using MektupApp.Application.Pricing;
using MektupApp.Domain.Loyalty;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

/// <summary>
/// Müşterinin mektup use case'leri. Bu servis iş kuralı içermez; aggregate'i yükler,
/// domain metodunu çağırır ve kaydeder (orchestration).
/// </summary>
public sealed class LetterService(
    ILetterRepository letters,
    ICustomerRepository customers,
    LetterPricingPolicy pricing,
    PricingService priceLists,
    LoyaltyService loyalty,
    ILoyaltyAccountRepository loyaltyAccounts,
    IPaymentGateway paymentGateway,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
{
    private static readonly HashSet<string> AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<LetterSummaryDto>> ListMineAsync(Guid customerId, CancellationToken ct = default)
    {
        var list = await letters.ListByCustomerAsync(customerId, ct);
        return list.Select(l => new LetterSummaryDto(
                l.Id, l.Recipient.FullName, l.Recipient.Type, l.Status,
                l.Content.Length > 120 ? l.Content[..120] + "…" : l.Content,
                l.UpdatedAt, l.TrackingCode))
            .ToList();
    }

    public async Task<LetterDetailsDto> GetAsync(Guid letterId, Guid customerId, CancellationToken ct = default) =>
        LetterDetailsDto.From(await LoadOwnedAsync(letterId, customerId, ct));

    public async Task<Guid> SaveDraftAsync(SaveLetterCommand cmd, CancellationToken ct = default)
    {
        var recipient = new Recipient(cmd.RecipientName, cmd.RecipientType, cmd.RecipientDetail, cmd.RecipientAddress.ToDomain());
        var senderAddress = cmd.SenderAddress.ToDomain();

        Letter letter;
        if (cmd.LetterId is { } id)
        {
            letter = await LoadOwnedAsync(id, cmd.CustomerId, ct);
            letter.UpdateEnvelope(cmd.SenderName, senderAddress, recipient);
        }
        else
        {
            letter = Letter.CreateDraft(cmd.CustomerId, cmd.SenderName, senderAddress, recipient);
            letters.Add(letter);
        }

        letter.WriteContent(cmd.Content);
        letter.ChangeStyle(new LetterStyle(cmd.Paper, cmd.Envelope, cmd.Font, cmd.Ink));
        letter.ChooseScent(cmd.Scent);
        letter.AttachPostcard(cmd.PostcardCategory is { } cat
            ? new Postcard(cat, cmd.PostcardDesign ?? 1)
            : null);

        await unitOfWork.SaveChangesAsync(ct);
        return letter.Id;
    }

    public async Task AddPhotoAsync(Guid letterId, Guid customerId, Stream content, string fileName, long length, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(ext))
            throw new AppException("Sadece JPG, PNG veya WEBP fotoğraf yükleyebilirsiniz.");
        if (length is <= 0 or > MaxPhotoBytes)
            throw new AppException("Fotoğraf boyutu en fazla 5 MB olabilir.");

        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        if (letter.Photos.Count >= Letter.MaxPhotos)
            throw new DomainException($"Bir mektuba en fazla {Letter.MaxPhotos} fotoğraf eklenebilir.");

        var key = await fileStorage.SaveAsync(content, ext, ct);
        try
        {
            letter.AddPhoto(key, Path.GetFileName(fileName));
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await fileStorage.DeleteAsync(key, ct);
            throw;
        }
    }

    public async Task RemovePhotoAsync(Guid letterId, Guid customerId, Guid photoId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        var removed = letter.RemovePhoto(photoId);
        await unitOfWork.SaveChangesAsync(ct);
        await fileStorage.DeleteAsync(removed.StorageKey, ct);
    }

    public async Task<(Stream Stream, string FileName)?> OpenPhotoAsync(Guid letterId, Guid photoId, Guid customerId, bool isAdmin, CancellationToken ct = default)
    {
        var letter = isAdmin
            ? await letters.GetAsync(letterId, ct) ?? throw new AppException("Mektup bulunamadı.")
            : await LoadOwnedAsync(letterId, customerId, ct);
        var photo = letter.Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null) return null;
        var stream = await fileStorage.OpenAsync(photo.StorageKey, ct);
        return stream is null ? null : (stream, photo.OriginalFileName);
    }

    public async Task<QuoteDto> GetQuoteAsync(Guid letterId, Guid customerId, PaymentMethod method, bool usePoints, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.EnsureReadyForCheckout();
        return (await BuildCheckoutAsync(letter, customerId, method, usePoints, ct)).Quote;
    }

    public async Task PayAsync(Guid letterId, Guid customerId, PaymentMethod method, bool usePoints, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.EnsureReadyForCheckout();

        var customer = await customers.GetAsync(customerId, ct) ?? throw new AppException("Müşteri bulunamadı.");
        var (quote, account) = await BuildCheckoutAsync(letter, customerId, method, usePoints, ct);

        // Tutarın tamamı puanla ödeniyorsa ödeme sağlayıcısına gidilmez.
        string reference;
        if (quote.AmountToPay > 0)
        {
            var result = await paymentGateway.ChargeAsync(
                new PaymentRequest(letter.Id, new Money(quote.AmountToPay), method, customer.Phone.Value), ct);
            if (!result.Succeeded)
                throw new AppException(result.Error ?? "Ödeme alınamadı. Lütfen tekrar deneyin.");
            reference = result.Reference!;
        }
        else
        {
            reference = $"PUAN-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        letter.MarkPaid(new Money(quote.AmountToPay), method, reference, quote.PointsUsed, quote.PointsToEarn);

        // Ödeme ile puan düşümü aynı işlemde (transaction) kaydedilir: biri olmadan diğeri olmamalı.
        // Hesapta eşzamanlı harcama olursa RowVersion çakışması ile ikinci işlem reddedilir.
        if (quote.PointsUsed > 0)
            (account ?? throw new AppException("Puan hesabı bulunamadı.")).Spend(letter.Id, quote.PointsUsed);

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Fiyatı hesaplar, puan kullanımını uygular ve kazanılacak puanı belirler.</summary>
    private async Task<(QuoteDto Quote, LoyaltyAccount? Account)> BuildCheckoutAsync(
        Letter letter, Guid customerId, PaymentMethod method, bool usePoints, CancellationToken ct)
    {
        var price = pricing.Calculate(letter, method, await priceLists.GetCurrentAsync(ct));
        var account = await loyaltyAccounts.GetByCustomerAsync(customerId, ct);
        var balance = account?.Balance ?? 0;

        var pointsUsed = usePoints ? Math.Min(balance, price.Total.Amount) : 0;
        var amountToPay = price.Total.Amount - pointsUsed;
        var settings = await loyalty.GetSettingsAsync(ct);
        var pointsToEarn = settings.CalculateReward(amountToPay);

        var quote = new QuoteDto(
            letter.Id, method,
            price.Lines.Select(l => (l.Description, l.Amount.Amount)).ToList(),
            price.Total.Amount,
            UsePoints: usePoints,
            PointsBalance: balance,
            PointsUsed: pointsUsed,
            AmountToPay: amountToPay,
            PointsToEarn: pointsToEarn,
            LoyaltyEnabled: settings.IsEnabled);
        return (quote, account);
    }

    public async Task CancelAsync(Guid letterId, Guid customerId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.Cancel("Müşteri tarafından iptal edildi.");
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid letterId, Guid customerId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.Delete();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ChangeRecipientAddressAsync(Guid letterId, Guid customerId, AddressDto address, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.ChangeRecipientAddress(address.ToDomain());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<TrackingResultDto?> TrackAsync(string trackingCode, CancellationToken ct = default)
    {
        string code;
        try { code = TrackingCode.Normalize(trackingCode); }
        catch (DomainException) { return null; }

        var letter = await letters.GetByTrackingCodeAsync(code, ct);
        if (letter is null) return null;

        // Takip sayfası herkese açık: alıcının kişisel bilgilerini gizle.
        var initials = string.Join(" ", letter.Recipient.FullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p[0] + "***"));

        return new TrackingResultDto(code, initials, letter.Recipient.Address.City, letter.Status,
            letter.History.Where(h => h.Status != LetterStatus.Draft)
                .Select(h => new StatusChangeDto(h.Status, h.Note, h.At)).ToList());
    }

    private async Task<Letter> LoadOwnedAsync(Guid letterId, Guid customerId, CancellationToken ct)
    {
        var letter = await letters.GetAsync(letterId, ct);
        if (letter is null || letter.IsDeleted || letter.CustomerId != customerId)
            throw new AppException("Mektup bulunamadı.");
        return letter;
    }
}

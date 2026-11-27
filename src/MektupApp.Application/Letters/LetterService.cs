using MektupApp.Application.Abstractions;
using MektupApp.Application.Payments;
using MektupApp.Domain.Common;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

/// <summary>
/// Müşterinin mektup use case'leri. Bu servis iş kuralı içermez; aggregate'i yükler,
/// domain metodunu çağırır ve kaydeder (orchestration).
/// </summary>
public sealed class LetterService(
    ILetterRepository letters,
    MektupApp.Domain.Postcards.IPostcardRepository postcards,
    MektupApp.Domain.Scents.IScentRepository scents,
    CheckoutCalculator checkout,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
{
    private static readonly HashSet<string> AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    public const long MaxPhotoBytes = 5 * 1024 * 1024;
    public const long MaxDocumentBytes = 10 * 1024 * 1024;
    public static readonly IReadOnlyList<string> AllowedDocumentExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    /// <summary>Alıcısı henüz girilmemiş taslağın listede görünen adı.</summary>
    public const string NoRecipientYet = "Alıcı henüz girilmedi";

    public async Task<IReadOnlyList<LetterSummaryDto>> ListMineAsync(Guid customerId, CancellationToken ct = default)
    {
        var list = await letters.ListByCustomerAsync(customerId, ct);
        return list.Select(l => new LetterSummaryDto(
                l.Id, l.Recipient.FullName.Length > 0 ? l.Recipient.FullName : NoRecipientYet, l.Recipient.Type, l.Status,
                l.Content.Length > 120 ? l.Content[..120] + "…" : l.Content,
                l.UpdatedAt, l.TrackingCode))
            .ToList();
    }

    /// <summary>
    /// Müşterinin bu türdeki (cezaevi / asker) en son mektubunun alıcı ve gönderen bilgileri; yoksa null.
    /// Aynı kişiye düzenli yazanlar her seferinde adresi tekrar girmesin diye yeni mektup formunu doldurur.
    /// </summary>
    public async Task<LetterPrefillDto?> GetPrefillAsync(Guid customerId, RecipientType type, CancellationToken ct = default)
    {
        if (type == RecipientType.Standard) return null;
        var last = (await letters.ListByCustomerAsync(customerId, ct))
            .Where(l => !l.IsDeleted && l.Recipient.Type == type && l.HasCompleteContacts)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefault();
        if (last is null) return null;
        var r = last.Recipient;
        return new LetterPrefillDto(type, r.FullName, r.Detail, r.FatherName, r.Ward, AddressDto.From(r.Address),
            last.SenderName, AddressDto.From(last.SenderAddress));
    }

    public async Task<LetterDetailsDto> GetAsync(Guid letterId, Guid customerId, CancellationToken ct = default) =>
        LetterDetailsDto.From(await LoadOwnedAsync(letterId, customerId, ct));

    public async Task<Guid> SaveDraftAsync(SaveLetterCommand cmd, CancellationToken ct = default)
    {
        // Taslak, alıcı / gönderen bilgileri eksikken de kaydedilebilir (ör. mektup yazılır yazılmaz);
        // eksiksiz olmaları ödemeye geçerken (Letter.EnsureReadyForCheckout) aranır.
        var recipient = Recipient.Draft(cmd.RecipientName, cmd.RecipientType, cmd.RecipientDetail, cmd.RecipientAddress.ToDraftDomain(),
            cmd.RecipientFatherName, cmd.RecipientWard);
        var senderAddress = cmd.SenderAddress.ToDraftDomain();

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
        letter.ChangeStyle(new LetterStyle(cmd.Paper, cmd.Envelope, cmd.Font, cmd.Ink, cmd.Size));
        letter.ChooseScent(await ResolveScentAsync(letter.Scent, cmd.ScentId, ct));
        letter.SetPostcards(await ResolvePostcardsAsync(letter.Postcards, cmd.PostcardIds, ct));

        await unitOfWork.SaveChangesAsync(ct);
        return letter.Id;
    }

    /// <summary>Seçilen koku aktif olmalı; mektupta zaten seçili koku sonradan pasife alındıysa seçim korunur.</summary>
    private async Task<LetterScent?> ResolveScentAsync(LetterScent? current, Guid? scentId, CancellationToken ct)
    {
        if (scentId is not { } id || id == Guid.Empty) return null;
        if (current?.ScentId == id) return current;
        var scent = await scents.GetAsync(id, ct);
        if (scent is not { IsActive: true })
            throw new AppException("Seçtiğiniz koku artık mevcut değil. Lütfen başka bir koku seçin.");
        return LetterScent.From(scent);
    }

    /// <summary>
    /// Seçilen kartpostallar katalogda olmalı ve aktif olmalı. Mektupta zaten seçili olan bir kartpostal sonradan pasife
    /// alındıysa müşteri mektubu düzenlerken o seçim kaybolmaz (anlık kopya korunur). Tekrarlanan seçim bir kez sayılır.
    /// </summary>
    private async Task<IReadOnlyList<Postcard>> ResolvePostcardsAsync(IReadOnlyList<Postcard> current, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var result = new List<Postcard>();
        foreach (var id in (ids ?? []).Where(i => i != Guid.Empty).Distinct())
        {
            if (current.FirstOrDefault(p => p.DesignId == id) is { } kept)
            {
                result.Add(kept);
                continue;
            }
            var design = await postcards.GetAsync(id, ct);
            if (design is not { IsActive: true })
                throw new AppException("Seçtiğiniz kartpostallardan biri artık mevcut değil. Lütfen seçiminizi kontrol edin.");
            result.Add(Postcard.From(design));
        }
        return result;
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

    /// <summary>
    /// Basılıp zarfa konacak belge ekler (PDF, JPG, PNG; en fazla 10 MB). Dosyanın içeriği uzantısıyla
    /// uyuşmalıdır (ör. adı .pdf olan bir dosya gerçekten PDF olmalı), aksi hâlde reddedilir.
    /// </summary>
    public async Task AddDocumentAsync(Guid letterId, Guid customerId, Stream content, string fileName, long length, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedDocumentExtensions.Contains(ext))
            throw new AppException("Sadece PDF, JPG veya PNG belge yükleyebilirsiniz.");
        if (length is <= 0 or > MaxDocumentBytes)
            throw new AppException("Belge boyutu en fazla 10 MB olabilir.");

        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        if (letter.Documents.Count >= Letter.MaxDocuments)
            throw new DomainException($"Bir mektuba en fazla {Letter.MaxDocuments} belge eklenebilir.");

        // İçerik imzasını kontrol edebilmek için belleğe al (boyut zaten sınırlı).
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxDocumentBytes)
            throw new AppException("Belge boyutu en fazla 10 MB olabilir.");
        if (!DocumentSignature.Matches(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), ext))
            throw new AppException("Dosya bozuk veya uzantısıyla uyuşmuyor. Lütfen geçerli bir PDF, JPG veya PNG yükleyin.");
        // Ağırlık hesabı için yaprak sayısı: PDF'in sayfa sayısı, görselde 1.
        var pageCount = ext == ".pdf" ? PdfPageCounter.Count(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) : 1;
        buffer.Position = 0;

        var key = await fileStorage.SaveAsync(buffer, ext, ct);
        try
        {
            letter.AddDocument(key, Path.GetFileName(fileName), pageCount);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await fileStorage.DeleteAsync(key, ct);
            throw;
        }
    }

    public async Task RemoveDocumentAsync(Guid letterId, Guid customerId, Guid documentId, CancellationToken ct = default)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        var removed = letter.RemoveDocument(documentId);
        await unitOfWork.SaveChangesAsync(ct);
        await fileStorage.DeleteAsync(removed.StorageKey, ct);
    }

    public async Task<(Stream Stream, string FileName)?> OpenDocumentAsync(Guid letterId, Guid documentId, Guid customerId, bool isAdmin, CancellationToken ct = default)
    {
        var letter = isAdmin
            ? await letters.GetAsync(letterId, ct) ?? throw new AppException("Mektup bulunamadı.")
            : await LoadOwnedAsync(letterId, customerId, ct);
        var document = letter.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return null;
        var stream = await fileStorage.OpenAsync(document.StorageKey, ct);
        return stream is null ? null : (stream, document.OriginalFileName);
    }

    public async Task<QuoteDto> GetQuoteAsync(Guid letterId, Guid customerId, PaymentMethod method, bool usePoints,
        CancellationToken ct = default, string? couponCode = null)
    {
        var letter = await LoadOwnedAsync(letterId, customerId, ct);
        letter.EnsureReadyForCheckout();
        return (await checkout.CalculateAsync(letter, method, usePoints, ct, couponCode)).Quote;
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

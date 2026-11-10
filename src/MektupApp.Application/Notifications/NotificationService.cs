using MektupApp.Application.Abstractions;
using MektupApp.Application.Admin;
using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;
using MektupApp.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace MektupApp.Application.Notifications;

public sealed record NotificationRowDto(
    Guid Id,
    Guid? CustomerId,
    NotificationChannel Channel,
    NotificationType Type,
    string Recipient,
    string? Subject,
    string Body,
    NotificationStatus Status,
    int Attempts,
    string? LastError,
    DateTime CreatedAt,
    DateTime? SentAt);

public sealed record NotificationSettingsDto(NotificationType SmsTypes, NotificationType EmailTypes,
    bool SmsSimulation, string SmsProvider, bool EmailSimulation, string EmailProvider);

/// <summary>Bildirimleri kuyruğa ekler: ayarlarda açık olan her kanal için bir kayıt oluşturur.</summary>
public sealed class NotificationService(
    INotificationRepository notifications,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork)
{
    public async Task EnqueueAsync(Guid customerId, NotificationContent content, CancellationToken ct = default)
    {
        var customer = await customers.GetAsync(customerId, ct);
        if (customer is null || !customer.IsActive) return;

        var settings = await GetSettingsAsync(ct);
        if (settings.IsEnabled(NotificationChannel.Sms, content.Type))
            notifications.Add(Notification.Sms(customerId, content.Type, customer.Phone.Value, content.SmsText));
        if (settings.IsEnabled(NotificationChannel.Email, content.Type))
            notifications.Add(Notification.Email(customerId, content.Type, customer.Email.Value,
                content.EmailSubject, content.EmailText, content.EmailHtml));
        await unitOfWork.SaveChangesAsync(ct);
    }

    internal async Task<NotificationSettings> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await notifications.GetSettingsAsync(ct);
        if (settings is not null) return settings;
        settings = NotificationSettings.CreateDefault();
        notifications.AddSettings(settings);
        return settings;
    }
}

/// <summary>Kuyruktaki bildirimleri gönderir (arka plan servisi çağırır). Hata olursa kayıt üzerinde yeniden deneme planlanır.</summary>
public sealed class NotificationDispatcher(
    INotificationRepository notifications,
    ISmsSender sms,
    IEmailSender email,
    IUnitOfWork unitOfWork,
    ILogger<NotificationDispatcher> logger)
{
    public async Task<int> SendDueAsync(int maxCount, CancellationToken ct = default)
    {
        var due = await notifications.ListDueAsync(DateTime.UtcNow, maxCount, ct);
        foreach (var n in due)
        {
            SendResult result;
            try
            {
                result = n.Channel == NotificationChannel.Sms
                    ? await sms.SendAsync(n.Recipient, n.Body, ct)
                    : await email.SendAsync(n.Recipient, n.Subject ?? "", n.Body, n.HtmlBody ?? n.Body, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Bildirim gönderilemedi: {Id}", n.Id);
                result = SendResult.Fail(ex.Message);
            }

            if (result.Success) n.MarkSent(result.MessageId);
            else n.MarkFailed(result.Error ?? "Bilinmeyen hata", result.Permanent);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return due.Count;
    }
}

/// <summary>Yönetim paneli: bildirim ayarları, gönderim geçmişi, yeniden deneme ve test gönderimi.</summary>
public sealed class NotificationAdminService(
    INotificationRepository notifications,
    NotificationService service,
    NotificationTemplates templates,
    ISmsSender sms,
    IEmailSender email,
    IUnitOfWork unitOfWork)
{
    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await service.GetSettingsAsync(ct);
        return new NotificationSettingsDto(s.SmsTypes, s.EmailTypes, sms.IsSimulation, sms.ProviderName, email.IsSimulation, email.ProviderName);
    }

    public async Task UpdateSettingsAsync(NotificationType smsTypes, NotificationType emailTypes, CancellationToken ct = default)
    {
        (await service.GetSettingsAsync(ct)).Update(smsTypes, emailTypes);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NotificationRowDto>> SearchAsync(NotificationChannel? channel, NotificationStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var (items, total) = await notifications.SearchAsync(channel, status, page, pageSize, ct);
        return new PagedResult<NotificationRowDto>(items.Select(n => new NotificationRowDto(n.Id, n.CustomerId, n.Channel, n.Type,
            n.Recipient, n.Subject, n.Body, n.Status, n.Attempts, n.LastError, n.CreatedAt, n.SentAt)).ToList(), page, pageSize, total);
    }

    public async Task<string?> GetEmailHtmlAsync(Guid id, CancellationToken ct = default) =>
        (await notifications.GetAsync(id, ct)) is { Channel: NotificationChannel.Email } n ? n.HtmlBody : null;

    public async Task RetryAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notifications.GetAsync(id, ct) ?? throw new AppException("Bildirim bulunamadı.");
        n.Retry();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Ayarları denemek için test mesajı kuyruğa ekler (birkaç saniye içinde gönderilir).</summary>
    public async Task SendTestAsync(NotificationChannel channel, string recipient, CancellationToken ct = default)
    {
        var content = templates.Test();
        if (channel == NotificationChannel.Sms)
        {
            PhoneNumber phone;
            try { phone = new PhoneNumber(recipient); }
            catch (DomainException ex) { throw new AppException(ex.Message); }
            notifications.Add(Notification.Sms(null, NotificationType.None, phone.Value, content.SmsText));
        }
        else
        {
            Email address;
            try { address = new Email(recipient); }
            catch (DomainException ex) { throw new AppException(ex.Message); }
            notifications.Add(Notification.Email(null, NotificationType.None, address.Value, content.EmailSubject, content.EmailText, content.EmailHtml));
        }
        await unitOfWork.SaveChangesAsync(ct);
    }
}

/// <summary>Üyelik ve mektup olaylarında müşteriye bildirim kuyruğa ekler.</summary>
public sealed class NotificationEventHandlers(
    NotificationService service,
    NotificationTemplates templates,
    ICustomerRepository customers,
    ILetterRepository letters) :
    IDomainEventHandler<CustomerRegistered>,
    IDomainEventHandler<LetterPaid>,
    IDomainEventHandler<LetterShipped>,
    IDomainEventHandler<LetterDelivered>,
    IDomainEventHandler<LetterCancelledAfterPayment>
{
    public async Task HandleAsync(CustomerRegistered e, CancellationToken ct)
    {
        var c = await customers.GetAsync(e.CustomerId, ct);
        if (c is not null) await service.EnqueueAsync(c.Id, templates.Welcome(c.FullName, c.ReferralCode), ct);
    }

    public Task HandleAsync(LetterPaid e, CancellationToken ct) =>
        WithLetterAsync(e.LetterId, e.CustomerId, (name, letter) => templates.OrderPaid(name, letter.Recipient.FullName, e.Amount, letter.Id), ct);

    public Task HandleAsync(LetterShipped e, CancellationToken ct) =>
        WithLetterAsync(e.LetterId, e.CustomerId, (name, letter) => templates.LetterShipped(name, letter.Recipient.FullName, e.TrackingCode), ct);

    public Task HandleAsync(LetterDelivered e, CancellationToken ct) =>
        WithLetterAsync(e.LetterId, e.CustomerId, (name, letter) => templates.LetterDelivered(name, letter.Recipient.FullName, letter.Id), ct);

    public Task HandleAsync(LetterCancelledAfterPayment e, CancellationToken ct) =>
        WithLetterAsync(e.LetterId, e.CustomerId, (name, letter) => templates.OrderCancelled(name, letter.Recipient.FullName, e.RefundAmount), ct);

    private async Task WithLetterAsync(Guid letterId, Guid customerId, Func<string, Letter, NotificationContent> build, CancellationToken ct)
    {
        var customer = await customers.GetAsync(customerId, ct);
        var letter = await letters.GetAsync(letterId, ct);
        if (customer is null || letter is null) return;
        await service.EnqueueAsync(customerId, build(customer.FullName, letter), ct);
    }
}

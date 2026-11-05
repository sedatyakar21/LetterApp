using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;
using MektupApp.Domain.Letters;

namespace MektupApp.Application.Letters;

/// <summary>Domain event'lerine tepki olarak müşteriye SMS gönderir.</summary>
public sealed class LetterNotificationHandlers(ICustomerRepository customers, ISmsSender sms) :
    IDomainEventHandler<LetterPaid>,
    IDomainEventHandler<LetterShipped>,
    IDomainEventHandler<LetterDelivered>,
    IDomainEventHandler<LetterCancelledAfterPayment>
{
    public Task HandleAsync(LetterPaid e, CancellationToken ct) =>
        NotifyAsync(e.CustomerId,
            $"Siparisiniz alindi ({(e.Amount > 0 ? $"{e.Amount:0.00} TL" : "MektupPuan ile odendi")}). Mektubunuz ilk is gununde postaya verilecek.", ct);

    public Task HandleAsync(LetterShipped e, CancellationToken ct) =>
        NotifyAsync(e.CustomerId, $"Mektubunuz yola cikti! Takip kodunuz: {e.TrackingCode}", ct);

    public Task HandleAsync(LetterDelivered e, CancellationToken ct) =>
        NotifyAsync(e.CustomerId, "Mektubunuz aliciya teslim edildi.", ct);

    public Task HandleAsync(LetterCancelledAfterPayment e, CancellationToken ct) =>
        NotifyAsync(e.CustomerId, $"Mektubunuz iptal edildi. {e.RefundAmount:0.00} TL iadeniz baslatildi.", ct);

    private async Task NotifyAsync(Guid customerId, string message, CancellationToken ct)
    {
        var customer = await customers.GetAsync(customerId, ct);
        if (customer is not null)
            await sms.SendAsync(customer.Phone.Value, message, ct);
    }
}

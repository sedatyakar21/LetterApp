using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;

namespace MektupApp.Application.Admin;

/// <summary>Yöneticinin müşteri hesapları üzerindeki işlemleri.</summary>
public sealed class CustomerAdminService(ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public Task BlockAsync(Guid customerId, Guid actingAdminId, CancellationToken ct = default) =>
        ExecuteAsync(customerId, actingAdminId, c => c.Block(), ct);

    public Task UnblockAsync(Guid customerId, Guid actingAdminId, CancellationToken ct = default) =>
        ExecuteAsync(customerId, actingAdminId, c => c.Unblock(), ct);

    public Task GrantAdminAsync(Guid customerId, Guid actingAdminId, CancellationToken ct = default) =>
        ExecuteAsync(customerId, actingAdminId, c => c.GrantAdmin(), ct);

    public Task RevokeAdminAsync(Guid customerId, Guid actingAdminId, CancellationToken ct = default) =>
        ExecuteAsync(customerId, actingAdminId, c => c.RevokeAdmin(), ct);

    /// <summary>Doğum tarihini düzeltir veya (null ile) siler. Yönetici kendi kaydında da yapabilir.</summary>
    public async Task SetBirthDateAsync(Guid customerId, DateOnly? birthDate, CancellationToken ct = default)
    {
        var customer = await customers.GetAsync(customerId, ct) ?? throw new AppException("Müşteri bulunamadı.");
        if (birthDate is { } d) customer.SetBirthDate(d, byAdmin: true);
        else customer.ClearBirthDate();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<DateOnly?> GetBirthDateAsync(Guid customerId, CancellationToken ct = default) =>
        (await customers.GetAsync(customerId, ct))?.BirthDate;

    private async Task ExecuteAsync(Guid customerId, Guid actingAdminId, Action<Customer> action, CancellationToken ct)
    {
        // Yöneticinin kendini kilitlemesini önle (son yönetici panele erişimini kaybetmesin).
        if (customerId == actingAdminId)
            throw new AppException("Kendi hesabınızda bu işlemi yapamazsınız.");

        var customer = await customers.GetAsync(customerId, ct) ?? throw new AppException("Müşteri bulunamadı.");
        action(customer);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

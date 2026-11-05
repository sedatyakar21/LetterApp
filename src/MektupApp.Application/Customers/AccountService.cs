using MektupApp.Application.Abstractions;
using MektupApp.Domain.Customers;

namespace MektupApp.Application.Customers;

public sealed record RegisterCommand(string FullName, string Email, string Phone, string Password);

public sealed record AuthenticatedCustomer(Guid Id, string FullName, string Email, bool IsAdmin);

public sealed class AccountService(ICustomerRepository customers, IPasswordHasher hasher, IUnitOfWork unitOfWork)
{
    public const int MinPasswordLength = 8;

    public async Task<AuthenticatedCustomer> RegisterAsync(RegisterCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(cmd.Password) || cmd.Password.Length < MinPasswordLength)
            throw new AppException($"Şifre en az {MinPasswordLength} karakter olmalıdır.");

        var email = new Email(cmd.Email);
        if (await customers.EmailExistsAsync(email, ct))
            throw new AppException("Bu e-posta adresi ile kayıtlı bir üyelik zaten var.");

        var customer = Customer.Register(cmd.FullName, email, new PhoneNumber(cmd.Phone), hasher.Hash(cmd.Password));
        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(ct);
        return ToAuth(customer);
    }

    public async Task<AuthenticatedCustomer?> AuthenticateAsync(string email, string password, CancellationToken ct = default)
    {
        Email parsed;
        try { parsed = new Email(email); }
        catch (Domain.Common.DomainException) { return null; }

        var customer = await customers.GetByEmailAsync(parsed, ct);
        if (customer is null || !hasher.Verify(customer.PasswordHash, password)) return null;
        if (!customer.IsActive)
            throw new AppException("Hesabınız askıya alınmıştır. Destek için bizimle iletişime geçin.");
        return ToAuth(customer);
    }

    /// <summary>Açık oturumların hâlâ geçerli olup olmadığını (engellenme / yetki değişimi) kontrol etmek için.</summary>
    public async Task<(bool IsActive, bool IsAdmin)?> GetAccessAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : (c.IsActive, c.IsAdmin);
    }

    public async Task<(string FullName, string Phone)?> GetProfileAsync(Guid customerId, CancellationToken ct = default)
    {
        var c = await customers.GetAsync(customerId, ct);
        return c is null ? null : (c.FullName, c.Phone.ToString());
    }

    private static AuthenticatedCustomer ToAuth(Customer c) => new(c.Id, c.FullName, c.Email.Value, c.IsAdmin);
}

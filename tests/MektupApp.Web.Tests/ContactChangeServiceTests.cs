using System.Text.RegularExpressions;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Application.Notifications;
using MektupApp.Domain.Customers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MektupApp.Web.Tests;

/// <summary>Hesabım → e-posta / cep telefonu değişikliği (yeni adrese onay kodu).</summary>
public class ContactChangeServiceTests
{
    private readonly Requests _requests = new();
    private readonly Customers _customers = new();
    private readonly Sender _sender = new();
    private readonly Customer _ali = Customer.Register("Ali Veli", new Email("ali@example.com"), new PhoneNumber("05551234567"), "hash:sifre123");
    private readonly Customer _ayse = Customer.Register("Ayşe Kaya", new Email("ayse@example.com"), new PhoneNumber("05559876543"), "hash:sifre456");

    public ContactChangeServiceTests()
    {
        _customers.Items.Add(_ali);
        _customers.Items.Add(_ayse);
    }

    private ContactChangeService Service() => new(_requests, _customers, new Hasher(), _sender, _sender,
        new NotificationTemplates(new NotificationOptions { SiteUrl = "https://bimektubum.com" }), new UnitOfWork(),
        NullLogger<ContactChangeService>.Instance);

    private static string CodeIn(string text) => Regex.Match(text, @"\b\d{6}\b").Value;

    [Fact]
    public async Task Email_change_sends_code_to_new_address_and_applies_after_confirm()
    {
        var pending = await Service().RequestEmailChangeAsync(_ali.Id, "Ali.Yeni@Example.com", "sifre123");
        Assert.Equal("ali.yeni@example.com", pending.Destination);
        var (to, body) = Assert.Single(_sender.Emails);
        Assert.Equal("ali.yeni@example.com", to);
        Assert.Equal("ali@example.com", _ali.Email.Value); // kod girilene kadar değişmez

        var auth = await Service().ConfirmAsync(_ali.Id, ContactChangeKind.Email, CodeIn(body));
        Assert.Equal("ali.yeni@example.com", _ali.Email.Value);
        Assert.Equal("ali.yeni@example.com", auth.Email);
        Assert.Empty(await Service().ListPendingAsync(_ali.Id));
    }

    [Fact]
    public async Task Phone_change_sends_sms_to_new_number()
    {
        await Service().RequestPhoneChangeAsync(_ali.Id, "0532 111 22 33", "sifre123");
        var (to, text) = Assert.Single(_sender.Sms);
        Assert.Equal("5321112233", to);
        Assert.Equal("5551234567", _ali.Phone.Value);

        await Service().ConfirmAsync(_ali.Id, ContactChangeKind.Phone, CodeIn(text));
        Assert.Equal("5321112233", _ali.Phone.Value);
        Assert.Equal("ali@example.com", _ali.Email.Value); // e-posta etkilenmez
    }

    [Theory]
    [InlineData(null)]
    [InlineData("yanlis")]
    public async Task Request_requires_current_password(string? password)
    {
        await Assert.ThrowsAsync<AppException>(() => Service().RequestEmailChangeAsync(_ali.Id, "yeni@example.com", password));
        Assert.Empty(_sender.Emails);
    }

    [Fact]
    public async Task Rejects_email_of_another_member_and_same_value()
    {
        await Assert.ThrowsAsync<AppException>(() => Service().RequestEmailChangeAsync(_ali.Id, "ayse@example.com", "sifre123"));
        await Assert.ThrowsAsync<AppException>(() => Service().RequestPhoneChangeAsync(_ali.Id, "0555 123 45 67", "sifre123"));
        Assert.Empty(_sender.Emails);
        Assert.Empty(_sender.Sms);
    }

    [Fact]
    public async Task Wrong_code_counts_attempts_then_locks()
    {
        await Service().RequestPhoneChangeAsync(_ali.Id, "05321112233", "sifre123");
        var right = CodeIn(_sender.Sms[0].Text);
        var wrong = right == "000000" ? "111111" : "000000";
        for (var i = 0; i < ContactChangeRequest.MaxFailedAttempts; i++)
            await Assert.ThrowsAsync<AppException>(() => Service().ConfirmAsync(_ali.Id, ContactChangeKind.Phone, wrong));
        // Hak bitti: doğru kod da artık geçmez.
        await Assert.ThrowsAsync<AppException>(() => Service().ConfirmAsync(_ali.Id, ContactChangeKind.Phone, right));
        Assert.Equal("5551234567", _ali.Phone.Value);
    }

    [Fact]
    public async Task New_request_invalidates_previous_code()
    {
        await Service().RequestEmailChangeAsync(_ali.Id, "ilk@example.com", "sifre123");
        var first = CodeIn(_sender.Emails[0].Body);
        await Service().RequestEmailChangeAsync(_ali.Id, "ikinci@example.com", "sifre123");
        var second = CodeIn(_sender.Emails[1].Body);
        if (first != second)
            await Assert.ThrowsAsync<AppException>(() => Service().ConfirmAsync(_ali.Id, ContactChangeKind.Email, first));
        await Service().ConfirmAsync(_ali.Id, ContactChangeKind.Email, second);
        Assert.Equal("ikinci@example.com", _ali.Email.Value);
    }

    [Fact]
    public async Task Email_taken_while_waiting_is_rejected_on_confirm()
    {
        await Service().RequestEmailChangeAsync(_ali.Id, "kapilan@example.com", "sifre123");
        var code = CodeIn(_sender.Emails[0].Body);
        _customers.Items.Add(Customer.Register("Başka", new Email("kapilan@example.com"), new PhoneNumber("05550000001"), "hash:x"));
        await Assert.ThrowsAsync<AppException>(() => Service().ConfirmAsync(_ali.Id, ContactChangeKind.Email, code));
        Assert.Equal("ali@example.com", _ali.Email.Value);
    }

    [Fact]
    public async Task Google_account_needs_no_password()
    {
        var google = Customer.RegisterWithGoogle("Can Er", new Email("can@gmail.com"), new PhoneNumber("05550001122"), "google-sub");
        _customers.Items.Add(google);
        await Service().RequestPhoneChangeAsync(google.Id, "05329990000", null);
        Assert.Single(_sender.Sms);
    }

    private sealed class Requests : IContactChangeRepository
    {
        public readonly List<ContactChangeRequest> Items = [];
        public Task<IReadOnlyList<ContactChangeRequest>> ListOpenAsync(Guid customerId, DateTime now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContactChangeRequest>>(Items.Where(r => r.CustomerId == customerId && r.ClosedAt == null && r.ExpiresAt > now).ToList());
        public Task<int> CountSinceAsync(Guid customerId, ContactChangeKind kind, DateTime since, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(r => r.CustomerId == customerId && r.Kind == kind && r.CreatedAt >= since));
        public void Add(ContactChangeRequest request) => Items.Add(request);
    }

    private sealed class Customers : ICustomerRepository
    {
        public readonly List<Customer> Items = [];
        public Task<Customer?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Id == id));
        public Task<Customer?> GetByEmailAsync(Email email, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(c => c.Email == email));
        public Task<Customer?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
        public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) => Task.FromResult(Items.Any(c => c.Email == email));
        public Task<Customer?> GetByReferralCodeAsync(string referralCode, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
        public Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountReferredAsync(Guid referrerId, CancellationToken ct = default) => Task.FromResult(0);
        public void Add(Customer customer) => Items.Add(customer);
    }

    private sealed class Sender : ISmsSender, IEmailSender
    {
        public readonly List<(string To, string Text)> Sms = [];
        public readonly List<(string To, string Body)> Emails = [];
        public bool IsSimulation => true;
        public string ProviderName => "test";
        public Task<SendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
        {
            Sms.Add((phoneNumber, message));
            return Task.FromResult(SendResult.Ok());
        }
        public Task<SendResult> SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default)
        {
            Emails.Add((to, textBody));
            return Task.FromResult(SendResult.Ok());
        }
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string hash, string password) => hash == "hash:" + password;
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

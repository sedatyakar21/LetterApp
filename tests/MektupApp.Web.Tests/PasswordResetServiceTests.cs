using System.Text.RegularExpressions;
using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Application.Notifications;
using MektupApp.Domain.Customers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MektupApp.Web.Tests;

public class PasswordResetServiceTests
{
    private readonly FakeResets _resets = new();
    private readonly FakeCustomers _customers = new();
    private readonly FakeSender _sender = new();
    private readonly Customer _ali;

    public PasswordResetServiceTests()
    {
        _ali = Customer.Register("Ali Veli", new Email("ali@example.com"), new PhoneNumber("05551234567"), "hash:eski-sifre");
        _customers.Items.Add(_ali);
    }

    private PasswordResetService Service() => new(_resets, _customers, new FakeHasher(), _sender, _sender,
        new NotificationTemplates(new NotificationOptions { SiteUrl = "https://bimektubum.com" }), new FakeUnitOfWork(),
        NullLogger<PasswordResetService>.Instance);

    private void Enable(bool email = true, bool sms = true)
    {
        var s = PasswordResetSettings.CreateDefault();
        s.Update(email, sms, "test");
        _resets.Settings = s;
    }

    [Fact]
    public async Task Default_settings_enable_email_only()
    {
        var o = await Service().GetOptionsAsync();
        Assert.True(o.EmailEnabled);
        Assert.False(o.SmsEnabled);
        await Assert.ThrowsAsync<AppException>(() => Service().RequestAsync("ali@example.com", PasswordResetMethod.Sms));
    }

    [Fact]
    public async Task Disabled_method_cannot_be_requested_or_used()
    {
        Enable(email: false, sms: false);
        await Assert.ThrowsAsync<AppException>(() => Service().RequestAsync("ali@example.com", PasswordResetMethod.Email));
        Assert.Empty(_sender.Emails);
    }

    [Fact]
    public async Task Email_link_resets_password_once_and_stores_only_hash()
    {
        Enable();
        await Service().RequestAsync("ALI@example.com", PasswordResetMethod.Email);

        var (to, body) = Assert.Single(_sender.Emails);
        Assert.Equal("ali@example.com", to);
        var token = Regex.Match(body, @"sifre-sifirla\?token=([A-Za-z0-9_-]+)").Groups[1].Value;
        Assert.NotEmpty(token);
        Assert.DoesNotContain(_resets.Requests, r => r.SecretHash.Contains(token));

        Assert.True(await Service().IsTokenValidAsync(token));
        await Service().ResetWithTokenAsync(token, "yeni-sifre-123");
        Assert.Equal("hash:yeni-sifre-123", _ali.PasswordHash);
        Assert.NotNull(_ali.PasswordChangedAt);

        // Tek kullanımlık.
        Assert.False(await Service().IsTokenValidAsync(token));
        await Assert.ThrowsAsync<AppException>(() => Service().ResetWithTokenAsync(token, "baska-sifre-123"));
    }

    [Fact]
    public async Task Unknown_email_sends_nothing_and_does_not_fail()
    {
        Enable();
        await Service().RequestAsync("yok@example.com", PasswordResetMethod.Email);
        Assert.Empty(_sender.Emails);
        Assert.Empty(_resets.Requests);
    }

    [Fact]
    public async Task Sms_code_resets_password_and_wrong_codes_are_limited()
    {
        Enable();
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Sms);
        var (phone, text) = Assert.Single(_sender.Sms);
        Assert.Equal("5551234567", phone);
        var code = Regex.Match(text, @"\b(\d{6})\b").Groups[1].Value;
        var wrong = code == "000000" ? "111111" : "000000";

        var ex = await Assert.ThrowsAsync<AppException>(() => Service().ResetWithCodeAsync("ali@example.com", wrong, "yeni-sifre-123"));
        Assert.Contains("4 deneme", ex.Message);

        await Service().ResetWithCodeAsync("ali@example.com", code[..3] + " " + code[3..], "yeni-sifre-123");
        Assert.Equal("hash:yeni-sifre-123", _ali.PasswordHash);
    }

    [Fact]
    public async Task Sms_code_is_locked_after_max_failed_attempts()
    {
        Enable();
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Sms);
        var code = Regex.Match(_sender.Sms[0].Text, @"\b(\d{6})\b").Groups[1].Value;
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < PasswordResetRequest.MaxFailedAttempts; i++)
            await Assert.ThrowsAsync<AppException>(() => Service().ResetWithCodeAsync("ali@example.com", wrong, "yeni-sifre-123"));

        // Doğru kod bile artık işe yaramaz.
        await Assert.ThrowsAsync<AppException>(() => Service().ResetWithCodeAsync("ali@example.com", code, "yeni-sifre-123"));
        Assert.Equal("hash:eski-sifre", _ali.PasswordHash);
    }

    [Fact]
    public async Task New_request_invalidates_previous_link_and_requests_are_rate_limited()
    {
        Enable();
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Email);
        var first = Regex.Match(_sender.Emails[0].Body, @"token=([A-Za-z0-9_-]+)").Groups[1].Value;
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Email);
        Assert.False(await Service().IsTokenValidAsync(first));

        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Email);
        await Assert.ThrowsAsync<AppException>(() => Service().RequestAsync("ali@example.com", PasswordResetMethod.Email));
        Assert.Equal(PasswordResetRequest.MaxRequestsPerWindow, _sender.Emails.Count);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        Enable();
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Email);
        var token = Regex.Match(_sender.Emails[0].Body, @"token=([A-Za-z0-9_-]+)").Groups[1].Value;
        await Assert.ThrowsAsync<AppException>(() => Service().ResetWithTokenAsync(token, "kisa"));
        Assert.True(await Service().IsTokenValidAsync(token));
    }

    [Fact]
    public async Task Blocked_customer_gets_no_reset()
    {
        Enable();
        _ali.Block();
        await Service().RequestAsync("ali@example.com", PasswordResetMethod.Email);
        Assert.Empty(_sender.Emails);
    }

    [Fact]
    public void Sessions_opened_before_password_change_are_rejected()
    {
        var changed = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
        var access = new CustomerAccess(true, false, changed);
        Assert.False(access.AllowsSessionFrom(new DateTimeOffset(changed.AddSeconds(-1))));
        Assert.True(access.AllowsSessionFrom(new DateTimeOffset(changed.AddSeconds(1))));
        Assert.True(access.AllowsSessionFrom(null));                          // eski oturum: tarih bilinmiyor
        Assert.True(new CustomerAccess(true, false, null).AllowsSessionFrom(DateTimeOffset.MinValue));
        Assert.False(new CustomerAccess(false, false, null).AllowsSessionFrom(DateTimeOffset.UtcNow));
    }

    // ---------------- sahteler ----------------

    private sealed class FakeResets : IPasswordResetRepository
    {
        public PasswordResetSettings? Settings;
        public readonly List<PasswordResetRequest> Requests = [];
        public Task<PasswordResetSettings?> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(Settings);
        public void AddSettings(PasswordResetSettings settings) => Settings = settings;
        public Task<PasswordResetRequest?> GetBySecretHashAsync(string secretHash, CancellationToken ct = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.SecretHash == secretHash));
        public Task<IReadOnlyList<PasswordResetRequest>> ListOpenAsync(Guid customerId, DateTime now, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PasswordResetRequest>>(Requests.Where(r => r.CustomerId == customerId && r.ClosedAt == null && r.ExpiresAt > now).ToList());
        public Task<int> CountSinceAsync(Guid customerId, DateTime since, CancellationToken ct = default) =>
            Task.FromResult(Requests.Count(r => r.CustomerId == customerId && r.CreatedAt >= since));
        public void Add(PasswordResetRequest request) => Requests.Add(request);
    }

    private sealed class FakeCustomers : ICustomerRepository
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

    private sealed class FakeSender : ISmsSender, IEmailSender
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

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string hash, string password) => hash == "hash:" + password;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}

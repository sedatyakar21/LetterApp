using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;

namespace MektupApp.Domain.Tests;

public class CustomerTests
{
    private static Customer NewCustomer(bool isAdmin = false) =>
        Customer.Register("Ali Veli", new Email("ali@example.com"), new PhoneNumber("0555 123 45 67"), "hash", isAdmin);

    [Fact]
    public void Google_account_has_no_password_and_cannot_unlink_google()
    {
        var c = Customer.RegisterWithGoogle("Ayşe Yılmaz", new Email("ayse@gmail.com"), new PhoneNumber("05551234567"), "google-sub-1");
        Assert.False(c.HasPassword);
        Assert.Equal("google-sub-1", c.GoogleSubject);
        Assert.Throws<DomainException>(() => c.UnlinkGoogle());
    }

    [Fact]
    public void Password_account_can_link_and_unlink_one_google_account()
    {
        var c = NewCustomer();
        c.LinkGoogle("sub-a");
        c.LinkGoogle("sub-a");                                   // aynı hesap tekrar: sorun yok
        Assert.Throws<DomainException>(() => c.LinkGoogle("sub-b"));
        c.UnlinkGoogle();
        Assert.Null(c.GoogleSubject);
        c.LinkGoogle("sub-b");
        Assert.Equal("sub-b", c.GoogleSubject);
    }

    [Fact]
    public void Birth_date_is_set_once_by_member_and_corrected_by_admin()
    {
        var c = NewCustomer();
        var birth = new DateOnly(1990, 5, 20);
        c.SetBirthDate(birth);
        c.SetBirthDate(birth);   // aynı tarih tekrar gönderilirse sorun yok
        Assert.Throws<DomainException>(() => c.SetBirthDate(new DateOnly(1991, 1, 1)));

        c.SetBirthDate(new DateOnly(1991, 1, 1), byAdmin: true);
        Assert.Equal(new DateOnly(1991, 1, 1), c.BirthDate);

        Assert.Throws<DomainException>(() => NewCustomer().SetBirthDate(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));
        Assert.Throws<DomainException>(() => NewCustomer().SetBirthDate(new DateOnly(1800, 1, 1)));
    }

    [Fact]
    public void Admin_cannot_be_blocked()
    {
        Assert.Throws<DomainException>(() => NewCustomer(isAdmin: true).Block());
    }

    [Fact]
    public void Blocked_customer_cannot_become_admin()
    {
        var c = NewCustomer();
        c.Block();
        Assert.False(c.IsActive);
        Assert.Throws<DomainException>(() => c.GrantAdmin());

        c.Unblock();
        c.GrantAdmin();
        Assert.True(c.IsAdmin);
    }

    [Fact]
    public void Phone_number_is_normalized()
    {
        Assert.Equal("5551234567", new PhoneNumber("+90 555 123 45 67").Value);
    }
}

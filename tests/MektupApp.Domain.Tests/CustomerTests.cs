using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;

namespace MektupApp.Domain.Tests;

public class CustomerTests
{
    private static Customer NewCustomer(bool isAdmin = false) =>
        Customer.Register("Ali Veli", new Email("ali@example.com"), new PhoneNumber("0555 123 45 67"), "hash", isAdmin);

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

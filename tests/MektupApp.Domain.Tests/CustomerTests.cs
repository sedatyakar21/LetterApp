using MektupApp.Domain.Common;
using MektupApp.Domain.Customers;

namespace MektupApp.Domain.Tests;

public class CustomerTests
{
    private static Customer NewCustomer(bool isAdmin = false) =>
        Customer.Register("Ali Veli", new Email("ali@example.com"), new PhoneNumber("0555 123 45 67"), "hash", isAdmin);

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

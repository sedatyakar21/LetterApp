using System.Globalization;

namespace MektupApp.Domain.Common;

/// <summary>Para değer nesnesi (value object). Değiştirilemez, değeriyle karşılaştırılır.</summary>
public sealed record Money
{
    public const string DefaultCurrency = "TRY";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0) throw new DomainException("Tutar negatif olamaz.");
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero { get; } = new(0);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);

    public static Money operator *(Money a, int count) => new(a.Amount * count, a.Currency);

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new DomainException("Farklı para birimleri toplanamaz.");
    }

    public override string ToString() => Currency == DefaultCurrency
        ? Amount.ToString("N2", Tr) + " ₺"
        : $"{Amount.ToString("N2", Tr)} {Currency}";
}

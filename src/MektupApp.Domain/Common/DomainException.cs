namespace MektupApp.Domain.Common;

/// <summary>Bir iş kuralı ihlal edildiğinde fırlatılır. Mesaj kullanıcıya gösterilebilir.</summary>
public class DomainException(string message) : Exception(message);

internal static class Guard
{
    public static string Required(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{fieldName} boş olamaz.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainException($"{fieldName} en fazla {maxLength} karakter olabilir.");

        return trimmed;
    }

    public static string? Optional(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Required(value, fieldName, maxLength);
    }
}

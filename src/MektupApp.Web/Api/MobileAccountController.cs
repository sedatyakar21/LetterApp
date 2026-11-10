using MektupApp.Application.Abstractions;
using MektupApp.Application.Customers;
using MektupApp.Application.Loyalty;
using MektupApp.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Api;

public sealed record MeDto(
    string FullName, string Email, string Phone, DateOnly MemberSince, DateOnly? BirthDate,
    string ReferralCode, int ReferredCount, bool GoogleLinked, decimal PointsBalance, decimal PointValue, bool LoyaltyEnabled);

public sealed record BirthDateRequest(DateOnly? BirthDate);

public sealed record PointTransactionApiDto(string Type, string TypeLabel, decimal Points, Guid? LetterId, string Note, DateTime At);

public sealed record PointsDto(
    decimal Balance, decimal BalanceValue, decimal TotalEarned, decimal PointValue,
    bool IsEnabled, string? EarnRule, bool ReferralEnabled, string? ReferralRule,
    string ReferralCode, int ReferredCount, IReadOnlyList<PointTransactionApiDto> Transactions);

/// <summary>Hesabım ve MektupPuan.</summary>
[Route("api/mobile/me")]
public sealed class MobileAccountController(AccountService accounts, LoyaltyService loyalty) : MobileApiController
{
    [HttpGet]
    public async Task<MeDto> Get(CancellationToken ct)
    {
        var a = await accounts.GetAccountAsync(CustomerId, ct) ?? throw new AppException("Üyelik bulunamadı.");
        var referral = await accounts.GetReferralInfoAsync(CustomerId, ct);
        var settings = await loyalty.GetSettingsAsync(ct);
        return new MeDto(a.FullName, a.Email, a.Phone, a.MemberSince, a.BirthDate, a.ReferralCode, referral?.ReferredCount ?? 0,
            a.GoogleLinked, await loyalty.GetBalanceAsync(CustomerId, ct), settings.PointValue, settings.IsEnabled);
    }

    /// <summary>Doğum tarihi bir kez girilir; sonra yalnızca yönetici değiştirebilir.</summary>
    [HttpPost("birth-date")]
    public async Task<IActionResult> BirthDate(BirthDateRequest req, CancellationToken ct)
    {
        if (req.BirthDate is null) throw new AppException("Lütfen doğum tarihinizi seçin.");
        await accounts.SetBirthDateAsync(CustomerId, req.BirthDate.Value, ct);
        return NoContent();
    }

    [HttpGet("points")]
    public async Task<PointsDto> Points(CancellationToken ct)
    {
        var account = await loyalty.GetAccountAsync(CustomerId, ct);
        var s = await loyalty.GetSettingsInfoAsync(ct);
        var referral = await accounts.GetReferralInfoAsync(CustomerId, ct);
        return new PointsDto(account.Balance, decimal.Round(account.Balance * s.PointValue, 2), account.TotalEarned, s.PointValue,
            s.IsEnabled, s.IsEnabled ? Rule(s.RewardType, s.Value, "Her mektubunuzda") : null,
            s.ReferralEnabled, s.ReferralEnabled ? Rule(s.ReferralRewardType, s.ReferralValue, "Davet ettiğiniz arkadaşlarınızın her mektubunda") : null,
            referral?.ReferralCode ?? "", referral?.ReferredCount ?? 0,
            account.Transactions.Select(t => new PointTransactionApiDto(t.Type.ToString(), t.Type.DisplayName(), t.Points, t.LetterId, t.Note, t.At)).ToList());
    }

    private static string Rule(Domain.Loyalty.RewardType type, decimal value, string prefix) =>
        type == Domain.Loyalty.RewardType.Percentage
            ? $"{prefix} ödenen tutar üzerinden %{value:0.##} oranında puan kazanırsınız."
            : $"{prefix} {value:0.##} puan kazanırsınız.";
}

using System.Globalization;
using MektupApp.Domain.Campaigns;

namespace MektupApp.Web.Infrastructure;

/// <summary>Kampanya tarihlerini ve kalan süreyi okunur Türkçe metne çevirir.</summary>
public static class CampaignText
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>"10 Kasım – 24 Kasım 2026", "10 Kasım 2026'dan itibaren" yerine ek gerektirmeyen biçimler.</summary>
    public static string DateRange(DateOnly start, DateOnly? end) => end switch
    {
        null => $"Başlangıç: {start.ToString("d MMMM yyyy", Tr)} · süresiz",
        { } e when e == start => e.ToString("d MMMM yyyy", Tr),
        { } e when e.Year == start.Year => $"{start.ToString("d MMMM", Tr)} – {e.ToString("d MMMM yyyy", Tr)}",
        { } e => $"{start.ToString("d MMMM yyyy", Tr)} – {e.ToString("d MMMM yyyy", Tr)}"
    };

    /// <summary>Kart köşesindeki durum etiketi.</summary>
    public static string Status(CampaignPhase phase, int? daysLeft, DateOnly start) => phase switch
    {
        CampaignPhase.Active when daysLeft == 0 => "Bugün son gün!",
        CampaignPhase.Active when daysLeft is > 0 and <= 7 => $"Son {daysLeft + 1} gün",
        CampaignPhase.Active => "Devam ediyor",
        CampaignPhase.Upcoming => $"Başlangıç: {start.ToString("d MMMM", Tr)}",
        CampaignPhase.Ended => "Sona erdi",
        _ => "Taslak"
    };

    /// <summary>Hedef kitle ve kullanım sınırının kısa açıklaması (ör. "Doğum gününüze özel · ±7 gün · 1 kez").</summary>
    public static string Audience(CampaignAudience audience, int? days, int? limit)
    {
        var text = audience switch
        {
            CampaignAudience.NewMembers => $"Yeni üyelere özel · üyeliğin ilk {days} günü",
            CampaignAudience.Birthday when days is null or 0 => "Doğum gününüze özel",
            CampaignAudience.Birthday => $"Doğum gününüze özel · ±{days} gün",
            _ => "Tüm üyeler"
        };
        if (limit is { } n)
            text += audience == CampaignAudience.Birthday ? $" · her doğum günü {n} kez" : $" · üye başına {n} kez";
        return text;
    }

    public static string StatusCss(CampaignPhase phase, int? daysLeft) => phase switch
    {
        CampaignPhase.Active when daysLeft is >= 0 and <= 7 => "campaign-status-urgent",
        CampaignPhase.Active => "campaign-status-active",
        CampaignPhase.Upcoming => "campaign-status-upcoming",
        _ => "campaign-status-ended"
    };
}

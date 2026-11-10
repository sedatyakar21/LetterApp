using MektupApp.Domain.Common;
using MektupApp.Domain.Notifications;

namespace MektupApp.Domain.Tests;

public class NotificationTests
{
    [Fact]
    public void Failed_send_is_retried_with_backoff_then_gives_up()
    {
        var n = Notification.Sms(null, NotificationType.OrderPaid, "5551234567", "Siparişiniz alındı");
        Assert.Equal(NotificationStatus.Pending, n.Status);

        n.MarkFailed("zaman aşımı");
        Assert.Equal(NotificationStatus.Pending, n.Status);
        Assert.True(n.NextAttemptAt > DateTime.UtcNow.AddSeconds(30));

        for (var i = 1; i < Notification.MaxAttempts; i++) n.MarkFailed("zaman aşımı");
        Assert.Equal(NotificationStatus.Failed, n.Status);
        Assert.Equal(Notification.MaxAttempts, n.Attempts);
    }

    [Fact]
    public void Permanent_error_fails_immediately_and_admin_can_retry()
    {
        var n = Notification.Email(null, NotificationType.Welcome, "a@b.com", "Hoş geldiniz", "metin", "<p>metin</p>");
        n.MarkFailed("geçersiz adres", permanent: true);
        Assert.Equal(NotificationStatus.Failed, n.Status);

        n.Retry();
        Assert.Equal(NotificationStatus.Pending, n.Status);
        Assert.Equal(0, n.Attempts);

        n.MarkSent("abc");
        Assert.Throws<DomainException>(() => n.Retry());
    }

    [Fact]
    public void Settings_enable_types_per_channel()
    {
        var s = NotificationSettings.CreateDefault();
        Assert.True(s.IsEnabled(NotificationChannel.Email, NotificationType.Welcome));
        Assert.False(s.IsEnabled(NotificationChannel.Sms, NotificationType.Welcome));

        s.Update(NotificationType.LetterShipped, NotificationType.None);
        Assert.True(s.IsEnabled(NotificationChannel.Sms, NotificationType.LetterShipped));
        Assert.False(s.IsEnabled(NotificationChannel.Sms, NotificationType.OrderPaid));
        Assert.False(s.IsEnabled(NotificationChannel.Email, NotificationType.LetterShipped));
    }
}

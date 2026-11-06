using MektupApp.Application.Abstractions;
using MektupApp.Domain.Letters;
using MektupApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MektupApp.Infrastructure.Carriers;

/// <summary>Taşıyıcı başına sıradaki barkod seri numarasını tutan sayaç (sadece altyapı tablosu).</summary>
public sealed class BarcodeCounter
{
    public string Carrier { get; set; } = string.Empty;
    public long NextSerial { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// PTT'nin tahsis ettiği aralıktan sıradaki barkodu üretir. Sayaç veritabanında tutulur;
/// eşzamanlı iki istek aynı numarayı alamaz (RowVersion çakışmasında yeniden denenir).
/// Kullanılmayan (ör. PTT'nin reddettiği) numaralar tekrar verilmez; bu kasıtlıdır.
/// </summary>
public sealed class BarcodeAllocator(AppDbContext db, IOptions<PttOptions> options) : IBarcodeAllocator
{
    private const string CounterKey = "PTT";
    private const int MaxAttempts = 5;

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var o = options.Value.Barcode;
        for (var attempt = 1; ; attempt++)
        {
            var counter = await db.Set<BarcodeCounter>().FirstOrDefaultAsync(c => c.Carrier == CounterKey, ct);
            if (counter is null)
            {
                counter = new BarcodeCounter { Carrier = CounterKey, NextSerial = o.RangeStart };
                db.Add(counter);
            }
            if (counter.NextSerial < o.RangeStart) counter.NextSerial = o.RangeStart; // aralık değiştiyse
            if (counter.NextSerial > o.RangeEnd)
                throw new AppException("PTT barkod aralığı doldu. PTT'den yeni aralık alıp Ptt:Barcode ayarlarını güncelleyin.");

            var serial = counter.NextSerial;
            counter.NextSerial++;
            try
            {
                await db.SaveChangesAsync(ct);
                return Format(o, serial);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Başka bir istek aynı anda sayacı ilerletti: güncel değeri okuyup tekrar dene.
                db.Entry(counter).State = EntityState.Detached;
            }
        }
    }

    internal static string Format(BarcodeOptions o, long serial) => o.Format.Equals("Cargo", StringComparison.OrdinalIgnoreCase)
        ? TrackingCode.Normalize($"{o.Prefix}{serial:00000000000}")
        : TrackingCode.BuildS10(o.Prefix, serial, o.Suffix);
}

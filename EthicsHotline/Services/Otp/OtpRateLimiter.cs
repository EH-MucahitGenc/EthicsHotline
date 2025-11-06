using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace EthicsHotline.Services.Otp;

public class OtpRateLimiter
{
    private readonly OtpOptions _opt;
    private readonly IMemoryCache? _mem;
    private readonly IConnectionMultiplexer? _redis;

    public OtpRateLimiter(OtpOptions opt, IMemoryCache? mem, IConnectionMultiplexer? redis)
    {
        _opt = opt; _mem = mem; _redis = redis;
    }

    private static string HourBucket() => DateTime.UtcNow.ToString("yyyyMMddHH");
    private static string KPhone(string phone) => $"otp:send:phone:{phone}:{HourBucket()}";
    private static string KClient(string client) => $"otp:send:client:{client}:{HourBucket()}";
    private static string KLast(string phone) => $"otp:{phone}:last";

    public async Task EnsureCanSendOrThrowAsync(string phone, string clientId, string? ip = null)
    {
        // resend cooldown (telefona bağlı)
        if (_redis is null)
        {
            var last = _mem?.Get<DateTimeOffset?>(KLast(phone));
            if (last.HasValue && DateTimeOffset.UtcNow - last.Value < _opt.ResendCooldown)
                throw new InvalidOperationException($"Lütfen {_opt.ResendCooldown.TotalSeconds:0} sn sonra tekrar deneyin.");
        }
        else
        {
            var db = _redis.GetDatabase();
            var txt = await db.StringGetAsync(KLast(phone));
            if (txt.HasValue && long.TryParse(txt.ToString(), out var ticks))
            {
                var last = new DateTimeOffset(ticks, TimeSpan.Zero);
                if (DateTimeOffset.UtcNow - last < _opt.ResendCooldown)
                    throw new InvalidOperationException($"Lütfen {_opt.ResendCooldown.TotalSeconds:0} sn sonra tekrar deneyin.");
            }
        }

        // saatlik limit — hem telefon hem client (oturum)
        var p = await IncrAsync(KPhone(phone));
        var c = await IncrAsync(KClient(clientId));

        if (p > _opt.MaxSendPerHour || c > _opt.MaxSendPerHour)
            throw new InvalidOperationException("SMS gönderim limitiniz bitti.");
    }

    public async Task MarkSentAsync(string phone)
    {
        if (_redis is null)
        {
            _mem?.Set(KLast(phone), DateTimeOffset.UtcNow, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });
        }
        else
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(KLast(phone), DateTimeOffset.UtcNow.Ticks, TimeSpan.FromHours(1));
        }
    }

    private async Task<int> IncrAsync(string key)
    {
        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var v = (int)await db.StringIncrementAsync(key);
            if (v == 1)
            {
                // içinde bulunulan saat bitene kadar
                var now = DateTime.UtcNow;
                var remain = TimeSpan.FromMinutes(59 - now.Minute)
                             + TimeSpan.FromSeconds(60 - now.Second);
                if (remain <= TimeSpan.Zero) remain = TimeSpan.FromHours(1);
                await db.KeyExpireAsync(key, remain);
            }
            return v;
        }

        if (!_mem!.TryGetValue<int>(key, out var cur)) cur = 0;
        var nv = cur + 1;
        _mem.Set(key, nv, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        });
        return nv;
    }
}

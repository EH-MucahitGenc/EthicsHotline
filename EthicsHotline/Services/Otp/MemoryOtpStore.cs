using Microsoft.Extensions.Caching.Memory;

namespace EthicsHotline.Services.Otp;

public class MemoryOtpStore : IOtpStore
{
    private readonly IMemoryCache _cache;
    public MemoryOtpStore(IMemoryCache cache) => _cache = cache;

    private static string Key(string phone) => $"otp:{phone}";
    private static string CntKey(string phone) => $"otp:{phone}:cnt";
    private static string VerKey(string phone) => $"otp:{phone}:ver";
    private static string LastKey(string phone) => $"otp:{phone}:last";

    public Task SaveAsync(string phone, string code, TimeSpan ttl)
    {
        _cache.Set(Key(phone), code, ttl);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string phone)
        => Task.FromResult(_cache.TryGetValue<string>(Key(phone), out var v) ? v : null);

    public Task<bool> ConsumeIfMatchAsync(string phone, string code)
    {
        if (_cache.TryGetValue<string>(Key(phone), out var v) && v == code)
        {
            _cache.Remove(Key(phone));
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> IncreaseSendCountAsync(string phone)
    {
        var k = CntKey(phone);
        var val = (_cache.Get<int?>(k) ?? 0) + 1;
        _cache.Set(k, val, TimeSpan.FromHours(1));
        return Task.FromResult(val);
    }

    public Task<int> GetSendCountAsync(string phone)
        => Task.FromResult(_cache.Get<int?>(CntKey(phone)) ?? 0);

    public Task<int> IncreaseVerifyAttemptsAsync(string phone)
    {
        var k = VerKey(phone);
        var val = (_cache.Get<int?>(k) ?? 0) + 1;
        _cache.Set(k, val, TimeSpan.FromHours(1));
        return Task.FromResult(val);
    }

    public Task<int> GetVerifyAttemptsAsync(string phone)
        => Task.FromResult(_cache.Get<int?>(VerKey(phone)) ?? 0);

    public Task<DateTimeOffset?> GetLastSentAsync(string phone)
        => Task.FromResult(_cache.Get<DateTimeOffset?>(LastKey(phone)));

    public Task SetLastSentAsync(string phone, DateTimeOffset when)
    {
        _cache.Set(LastKey(phone), when, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.Caching.Memory;

namespace EthicsHotline.Services.Otp;

public sealed class MemoryOtpStore : IOtpStore
{
    private readonly IMemoryCache _cache;
    public MemoryOtpStore(IMemoryCache cache) => _cache = cache;

    public Task<OtpState?> GetAsync(string key) =>
        Task.FromResult(_cache.TryGetValue(key, out OtpState state) ? state : null);

    public Task SetAsync(string key, OtpState state, TimeSpan ttl)
    {
        _cache.Set(key, state, ttl);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key)
    {
        _cache.Remove(key);
        return Task.FromResult(true);
    }
}

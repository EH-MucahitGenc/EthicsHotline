using System.Text.Json;
using StackExchange.Redis;

namespace EthicsHotline.Services.Otp;

public sealed class RedisOtpStore : IOtpStore
{
    private readonly IConnectionMultiplexer _redis;
    public RedisOtpStore(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<OtpState?> GetAsync(string key)
    {
        var db = _redis.GetDatabase();
        var val = await db.StringGetAsync(key);
        return val.HasValue ? JsonSerializer.Deserialize<OtpState>(val!) : null;
    }

    public async Task SetAsync(string key, OtpState state, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, JsonSerializer.Serialize(state), ttl);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        var db = _redis.GetDatabase();
        return await db.KeyDeleteAsync(key);
    }
}

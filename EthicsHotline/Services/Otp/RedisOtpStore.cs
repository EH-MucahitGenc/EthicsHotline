using StackExchange.Redis;

namespace EthicsHotline.Services.Otp;

public class RedisOtpStore : IOtpStore
{
    private readonly IConnectionMultiplexer _mux;
    public RedisOtpStore(IConnectionMultiplexer mux) => _mux = mux;

    private static string Key(string p) => $"otp:{p}";
    private static string CntKey(string p) => $"otp:{p}:cnt";
    private static string VerKey(string p) => $"otp:{p}:ver";
    private static string LastKey(string p) => $"otp:{p}:last";

    public async Task SaveAsync(string phone, string code, TimeSpan ttl)
    {
        var db = _mux.GetDatabase();
        await db.StringSetAsync(Key(phone), code, ttl);
    }

    public async Task<string?> GetAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringGetAsync(Key(phone));
        return v.HasValue ? v.ToString() : null;
    }

    public async Task<bool> ConsumeIfMatchAsync(string phone, string code)
    {
        var db = _mux.GetDatabase();
        var tran = db.CreateTransaction();
        var key = Key(phone);
        var cond = tran.AddCondition(Condition.StringEqual(key, code));
        _ = tran.KeyDeleteAsync(key);
        var committed = await tran.ExecuteAsync();
        return committed && cond.WasSatisfied;
    }

    public async Task<int> IncreaseSendCountAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringIncrementAsync(CntKey(phone));
        await db.KeyExpireAsync(CntKey(phone), TimeSpan.FromHours(1));
        return (int)v;
    }

    public async Task<int> GetSendCountAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringGetAsync(CntKey(phone));
        return v.HasValue ? (int)v : 0;
    }

    public async Task<int> IncreaseVerifyAttemptsAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringIncrementAsync(VerKey(phone));
        await db.KeyExpireAsync(VerKey(phone), TimeSpan.FromHours(1));
        return (int)v;
    }

    public async Task<int> GetVerifyAttemptsAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringGetAsync(VerKey(phone));
        return v.HasValue ? (int)v : 0;
    }

    public async Task<DateTimeOffset?> GetLastSentAsync(string phone)
    {
        var db = _mux.GetDatabase();
        var v = await db.StringGetAsync(LastKey(phone));
        if (!v.HasValue) return null;
        if (long.TryParse(v.ToString(), out var ticks)) return new DateTimeOffset(ticks, TimeSpan.Zero);
        return null;
    }

    public async Task SetLastSentAsync(string phone, DateTimeOffset when)
    {
        var db = _mux.GetDatabase();
        await db.StringSetAsync(LastKey(phone), when.Ticks, TimeSpan.FromHours(1));
    }
}

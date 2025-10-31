namespace EthicsHotline.Services.Otp;

public interface IOtpStore
{
    Task<OtpState?> GetAsync(string key);
    Task SetAsync(string key, OtpState state, TimeSpan ttl);
    Task<bool> DeleteAsync(string key);
}

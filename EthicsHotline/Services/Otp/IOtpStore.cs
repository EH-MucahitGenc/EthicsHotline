namespace EthicsHotline.Services.Otp;

public interface IOtpStore
{
    Task SaveAsync(string phone, string code, TimeSpan ttl);
    Task<string?> GetAsync(string phone);
    Task<bool> ConsumeIfMatchAsync(string phone, string code);
    Task<int> IncreaseSendCountAsync(string phone);
    Task<int> GetSendCountAsync(string phone);
    Task<int> IncreaseVerifyAttemptsAsync(string phone);
    Task<int> GetVerifyAttemptsAsync(string phone);
    Task<DateTimeOffset?> GetLastSentAsync(string phone);
    Task SetLastSentAsync(string phone, DateTimeOffset when);
}

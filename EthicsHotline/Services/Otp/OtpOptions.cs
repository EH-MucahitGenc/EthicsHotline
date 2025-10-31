namespace EthicsHotline.Services.Otp;

public sealed class OtpOptions
{
    public int Digits { get; set; } = 6;
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxSendPerHour { get; set; } = 5;
    public int MaxVerifyAttempts { get; set; } = 5;
}

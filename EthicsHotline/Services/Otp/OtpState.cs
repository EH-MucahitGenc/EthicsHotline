namespace EthicsHotline.Services.Otp;

public sealed class OtpState
{
    public string? OtpHash { get; set; }
    public string? Salt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public int SentCount { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }
    public DateTimeOffset? SentWindowStart { get; set; }
}

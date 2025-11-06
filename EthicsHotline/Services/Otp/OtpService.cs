using System.Security.Cryptography;
using System.Text.RegularExpressions;
using EthicsHotline.Services.Sms;

namespace EthicsHotline.Services.Otp;

public class OtpService
{
    private readonly IOtpStore _store;
    private readonly ISmsSender _sms;
    private readonly OtpOptions _opt;
    private readonly OtpRateLimiter _limiter;

    public OtpService(IOtpStore store, ISmsSender sms, OtpOptions opt, OtpRateLimiter limiter)
    {
        _store = store; _sms = sms; _opt = opt; _limiter = limiter;
    }

    private static bool IsValidE164Tr(string phone)
        => Regex.IsMatch(phone ?? "", @"^\+905\d{9}$");

    private static string GenerateDigits(int n)
    {
        var max = (int)Math.Pow(10, n);
        var num = RandomNumberGenerator.GetInt32(0, max);
        return num.ToString(new string('0', n));
    }

    // Normal mod: limit kontrolü (telefon+client), üret, store, SMS gönder
    public async Task<string> SendAsync(string phone, string clientId, string? ip)
    {
        if (!IsValidE164Tr(phone)) throw new ArgumentException("Geçersiz telefon formatı (+905XXXXXXXXX).");

        await _limiter.EnsureCanSendOrThrowAsync(phone, clientId, ip);

        var code = GenerateDigits(_opt.Digits);
        await _store.SaveAsync(phone, code, _opt.Ttl);

        await _sms.SendAsync(phone, $"Doğrulama kodunuz: {code}");

        await _limiter.MarkSentAsync(phone);
        return code;
    }

    // Mirror-to-email: limit kontrolü (telefon+client), üret, store, SMS YOK
    public async Task<string> GenerateOnlyAsync(string phone, string clientId, string? ip)
    {
        if (!IsValidE164Tr(phone)) throw new ArgumentException("Geçersiz telefon formatı (+905XXXXXXXXX).");

        await _limiter.EnsureCanSendOrThrowAsync(phone, clientId, ip);

        var code = GenerateDigits(_opt.Digits);
        await _store.SaveAsync(phone, code, _opt.Ttl);

        await _limiter.MarkSentAsync(phone);
        return code;
    }

    public async Task<bool> VerifyAsync(string phone, string code, bool consumeIfValid)
    {
        if (!IsValidE164Tr(phone)) return false;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var attempts = await _store.IncreaseVerifyAttemptsAsync(phone);
        if (attempts > _opt.MaxVerifyAttempts) return false;

        if (consumeIfValid)
            return await _store.ConsumeIfMatchAsync(phone, code);

        var current = await _store.GetAsync(phone);
        return string.Equals(current, code, StringComparison.Ordinal);
    }
}

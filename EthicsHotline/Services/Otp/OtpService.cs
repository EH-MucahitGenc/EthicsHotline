using System.Security.Cryptography;
using System.Text;

namespace EthicsHotline.Services.Otp;

public sealed class OtpService
{
    private readonly IOtpStore _store;
    private readonly Sms.ISmsSender _sms;
    private readonly OtpOptions _opt;

    public OtpService(IOtpStore store, Sms.ISmsSender sms, OtpOptions opt)
    { _store = store; _sms = sms; _opt = opt; }

    private static string Key(string phone) => $"otp:{NormalizePhoneE164(phone)}";

    public async Task<string> SendAsync(string phone, string? ip)
    {
        phone = NormalizePhoneE164(phone);
        var key = Key(phone);
        var now = DateTimeOffset.UtcNow;

        var state = await _store.GetAsync(key) ?? new OtpState();

        if (state.LastSentAt.HasValue && now - state.LastSentAt < _opt.ResendCooldown)
            throw new InvalidOperationException("Tekrar gönderim için lütfen bekleyiniz.");

        if (state.SentWindowStart == null || now - state.SentWindowStart > TimeSpan.FromHours(1))
        { state.SentWindowStart = now; state.SentCount = 0; }
        if (state.SentCount >= _opt.MaxSendPerHour)
            throw new InvalidOperationException("Saatlik gönderim limiti aşıldı.");

        var otp = GenerateNumericOtp(_opt.Digits);
        var salt = GenerateSalt(16);
        var hash = HashOtp(otp, salt);

        state.Salt = Convert.ToBase64String(salt);
        state.OtpHash = Convert.ToBase64String(hash);
        state.ExpiresAt = now.Add(_opt.Ttl);
        state.Attempts = 0;
        state.LastSentAt = now;
        state.SentCount += 1;

        await _store.SetAsync(key, state, _opt.Ttl);

        // SMS (Mock ya da gerçek)
        await _sms.SendAsync(phone, $"Doğrulama kodunuz: {otp}. {_opt.Ttl.TotalMinutes:F0} dk içinde kullanınız.");

        return otp;
    }

    public async Task<bool> VerifyAsync(string phone, string code, bool consumeIfValid)
    {
        phone = NormalizePhoneE164(phone);
        var key = Key(phone);
        var state = await _store.GetAsync(key);
        if (state is null) return false;

        var now = DateTimeOffset.UtcNow;
        if (state.ExpiresAt is null || now > state.ExpiresAt) { await _store.DeleteAsync(key); return false; }
        if (state.Attempts >= _opt.MaxVerifyAttempts) { await _store.DeleteAsync(key); return false; }

        state.Attempts += 1;

        var salt = Convert.FromBase64String(state.Salt!);
        var expected = Convert.FromBase64String(state.OtpHash!);
        var ok = FixedTimeEquals(HashOtp(code, salt), expected);

        if (ok)
        {
            if (consumeIfValid) await _store.DeleteAsync(key);
            else await _store.SetAsync(key, state, state.ExpiresAt.Value - now);
            return true;
        }

        await _store.SetAsync(key, state, state.ExpiresAt.Value - now);
        if (state.Attempts >= _opt.MaxVerifyAttempts) await _store.DeleteAsync(key);
        return false;
    }

    // Helpers
    private static string NormalizePhoneE164(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90")) return "+" + digits;
        if (digits.StartsWith("0") && digits.Length == 11) return "+90" + digits[1..];
        if (digits.Length == 10) return "+90" + digits;
        return phone.StartsWith("+") ? phone : "+" + digits;
    }

    private static string GenerateNumericOtp(int digits)
    {
        using var rng = RandomNumberGenerator.Create();
        var b = new byte[4]; rng.GetBytes(b);
        var n = BitConverter.ToUInt32(b, 0) % (uint)Math.Pow(10, digits);
        return n.ToString(new string('0', digits));
    }

    private static byte[] GenerateSalt(int size) { var s = new byte[size]; RandomNumberGenerator.Fill(s); return s; }
    private static byte[] HashOtp(string otp, byte[] salt) { using var h = new HMACSHA256(salt); return h.ComputeHash(Encoding.UTF8.GetBytes(otp)); }
    private static bool FixedTimeEquals(byte[] a, byte[] b) => CryptographicOperations.FixedTimeEquals(a, b);
}

namespace EthicsHotline.Services.Sms;

public sealed class PoliSmsSenderAdapter : ISmsSender
{
    private readonly IPoliDijitalClient _client;
    private readonly PoliDijitalOptions _opt;

    public PoliSmsSenderAdapter(IPoliDijitalClient client, Microsoft.Extensions.Options.IOptions<PoliDijitalOptions> opt)
    {
        _client = client;
        _opt = opt.Value;
    }

    public async Task SendAsync(string phoneE164, string message, CancellationToken ct = default)
    {
        var digits = new string(phoneE164.Where(char.IsDigit).ToArray()); // +90... → 90...
        if (string.IsNullOrWhiteSpace(digits) || digits.Length < 11)
            throw new ArgumentException("Geçersiz telefon.", nameof(phoneE164));

        var req = new SendSingleSms
        {
            Type = 1,
            Title = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            Content = message,
            Number = long.Parse(digits),
            Encoding = _opt.Encoding,
            Sender = _opt.Sender,
            Gateway = _opt.Gateway ?? "",
            Validity = _opt.Validity,
            Commercial = _opt.Commercial,
            SkipAhsQuery = _opt.SkipAhsQuery
        };

        var res = await _client.SendSingleAsync(req, ct);
        if (res.Err is not null)
            throw new InvalidOperationException($"SMS hatası: Lütfen daha sonra tekrar deneyin");
    }
}

namespace EthicsHotline.Services.Sms;

public sealed class NetGsmSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    public NetGsmSender(HttpClient http, IConfiguration cfg) { _http = http; _cfg = cfg; }

    public async Task SendAsync(string phoneE164, string message, CancellationToken ct = default)
    {
        // TODO: NetGSM API dökümanına göre payload'ı hazırlayıp gönder.
        // _cfg["Sms:NetGsm:Username"], _cfg["Sms:NetGsm:Password"], _cfg["Sms:From"] kullan.
        await Task.CompletedTask;
    }
}

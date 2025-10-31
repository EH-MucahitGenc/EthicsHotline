using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EthicsHotline.Services.Sms;

public sealed class PoliDijitalSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PoliDijitalSender(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;

        // ---- Base URL
        var host = _cfg["Sms:PoliDijital:Host"] ?? throw new InvalidOperationException("Sms:PoliDijital:Host yok");
        var port = int.TryParse(_cfg["Sms:PoliDijital:Port"], out var p) ? p : 9587;
        var scheme = (p == 9588) ? "https" : "http";
        _baseUrl = $"{scheme}://{host}:{p}/";

        _http.BaseAddress = new Uri(_baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(30);

        // ---- TLS ve Basic Auth
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        var user = _cfg["Sms:PoliDijital:Username"] ?? throw new InvalidOperationException("Sms:PoliDijital:Username yok");
        var pass = _cfg["Sms:PoliDijital:Password"] ?? throw new InvalidOperationException("Sms:PoliDijital:Password yok");
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", raw);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task SendAsync(string phoneE164, string message, CancellationToken ct = default)
    {
        // Poli “single sms” -> POST sms/create
        var number = NormalizeMsisdnLong(phoneE164); // 905XXXXXXXXX (long)
        if (number == 0) throw new ArgumentException("Geçersiz MSISDN");

        var sender = _cfg["Sms:From"] ?? throw new InvalidOperationException("Sms:From (başlık) yok");
        var encoding = int.TryParse(_cfg["Sms:PoliDijital:Encoding"], out var enc) ? enc : 0;
        var validity = int.TryParse(_cfg["Sms:PoliDijital:Validity"], out var val) ? val : 60;
        var commercial = bool.TryParse(_cfg["Sms:PoliDijital:Commercial"], out var com) && com;
        var skipAhs = bool.TryParse(_cfg["Sms:PoliDijital:SkipAhsQuery"], out var sk) && sk;
        var gateway = _cfg["Sms:PoliDijital:Gateway"];

        var payload = new
        {
            // Poli alan eşlemesi (Single)
            type = 1,                       // 1: Standart SMS (ihtiyaca göre)
            sendingType = 0,                // 0: Single
            title = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            content = message,
            number = number,
            encoding = encoding,
            sender = sender,
            validity = validity,            // dk (60..1440 arası mantıklı)
            commercial = commercial,
            skipAhsQuery = skipAhs,
            customID = (string?)null,
            gateway = string.IsNullOrWhiteSpace(gateway) ? null : gateway,
            sendingDate = (string?)null,    // anlık gönderim
            pushSettings = (object?)null
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "sms/create")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };

        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);

        // Beklenen cevap: {"data":{"pkgID":12345},"err":null}  veya {"data":null,"err":{...}}
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind != JsonValueKind.Null)
        {
            // başarı
            return;
        }

        // hata
        var errEl = doc.RootElement.GetProperty("err");
        var status = errEl.TryGetProperty("status", out var st) ? st.GetInt32() : (int?)null;
        var code = errEl.TryGetProperty("code", out var cd) ? cd.GetString() : null;
        var msg = errEl.TryGetProperty("message", out var ms) ? ms.GetString() : "Poli Dijital SMS hatası";
        throw new InvalidOperationException($"PoliDijital [{status}:{code}] {msg}");
    }

    // +905XXXXXXXXX / 05XXXXXXXXX / 5XXXXXXXXX -> 905XXXXXXXXX (long)
    private static long NormalizeMsisdnLong(string phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());

        if (digits.StartsWith("90") && digits.Length == 12) return long.Parse(digits);
        if (digits.StartsWith("0") && digits.Length == 11 && digits[1] == '5') return long.Parse("90" + digits[1..]);
        if (digits.Length == 10 && digits[0] == '5') return long.Parse("90" + digits);
        if (digits.StartsWith("905") && digits.Length == 12) return long.Parse(digits);

        // Son çare: + li gelmişse +’yı at ve dene
        if (phone.StartsWith("+") && digits.StartsWith("90") && digits.Length == 12) return long.Parse(digits);

        return 0;
    }
}

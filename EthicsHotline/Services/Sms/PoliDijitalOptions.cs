namespace EthicsHotline.Services.Sms;

public sealed class PoliDijitalOptions
{
    public string Host { get; set; } = "app.polidijital.com"; // sadece domain
    public int Port { get; set; } = 9588;                     // 9588: HTTPS, 9587: HTTP
    public string Username { get; set; } = "politestotp";
    public string Password { get; set; } = "heeatdup*4";
    public string Sender { get; set; } = "POLITELEKOM";                    // onaylı başlık
    public string? Gateway { get; set; }                      // uuid gerekirse
    public int Encoding { get; set; } = 0;                    // 0: GSM, 1: Unicode
    public int Validity { get; set; } = 60;                   // dakika (60..1440)
    public bool Commercial { get; set; } = false;
    public bool SkipAhsQuery { get; set; } = true;
}

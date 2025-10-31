using System.Net;
using System.Net.Mail;

namespace EthicsHotline.Services.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _cfg;
    public SmtpEmailSender(IConfiguration cfg) => _cfg = cfg;

    public async Task SendAsync(string to, string subject, string body, bool isHtml = false)
    {
        var host = _cfg["Mail:Host"] ?? "smtp.office365.com";
        var port = int.TryParse(_cfg["Mail:Port"], out var p) ? p : 587;
        var enableSsl = bool.TryParse(_cfg["Mail:EnableSsl"], out var ssl) ? ssl : true;
        var from = _cfg["Mail:From"]!;
        var user = _cfg["Mail:Username"]!;
        var pass = _cfg["Mail:Password"]!;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(user, pass)
        };

        using var msg = new MailMessage(from, to, subject, body) { IsBodyHtml = isHtml };
        await client.SendMailAsync(msg);
    }
}
